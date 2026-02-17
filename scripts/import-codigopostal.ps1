param(
    [string]$CsvPath = ".\codigopostal.csv",
    [string]$ConnectionString = "",
    [switch]$DetailedLog,
    [switch]$DryRun,
    [switch]$OnlyStartWith9,
    [switch]$CompareOnly,
    [string]$SourceConnectionString = "",
    [string]$SourceTable = "CodigosPostais",
    [string]$TargetTable = "CodigosPostais"
)

$ErrorActionPreference = "Stop"

function Log-Info {
    param([string]$Message)
    Write-Host "[INFO ] $Message"
}

function Log-Warn {
    param([string]$Message)
    Write-Host "[WARN ] $Message" -ForegroundColor Yellow
}

function Log-Debug {
    param([string]$Message)
    if ($DetailedLog) {
        Write-Host "[DEBUG] $Message" -ForegroundColor DarkGray
    }
}

function Get-ConnectionStringFromAppSettings {
    param(
        [string]$RepoRoot
    )

    $appSettingsPath = Join-Path $RepoRoot "src\REA.Emergencia.Web\appsettings.json"
    if (-not (Test-Path $appSettingsPath)) {
        throw "Nao foi possivel encontrar appsettings.json em $appSettingsPath"
    }

    $json = Get-Content -Path $appSettingsPath -Raw | ConvertFrom-Json
    $conn = $json.ConnectionStrings.DefaultConnection
    if ([string]::IsNullOrWhiteSpace($conn)) {
        throw "ConnectionStrings:DefaultConnection nao esta definido no appsettings.json."
    }

    return [string]$conn
}

function Normalize-Text {
    param([string]$Value)
    if ($null -eq $Value) { return "" }

    $trimmed = $Value.Trim()
    if ($trimmed.Length -eq 0) { return "" }

    $formD = $trimmed.Normalize([Text.NormalizationForm]::FormD)
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $formD.ToCharArray()) {
        $cat = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch)
        if ($cat -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$sb.Append($ch)
        }
    }

    return $sb.ToString().Normalize([Text.NormalizationForm]::FormC).ToUpperInvariant()
}

function Get-SafeQualifiedTableName {
    param([string]$TableName)

    if ([string]::IsNullOrWhiteSpace($TableName)) {
        throw "Nome de tabela vazio."
    }

    $trimmed = $TableName.Trim()
    if ($trimmed -notmatch '^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$') {
        throw "Nome de tabela invalido: '$TableName'. Use 'Tabela' ou 'Schema.Tabela'."
    }

    if ($trimmed.Contains('.')) {
        $parts = $trimmed.Split('.', 2)
        return "[$($parts[0])].[$($parts[1])]"
    }

    return "[dbo].[$trimmed]"
}

function Read-CodigosPostaisTable {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$QualifiedTableName
    )

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = "SELECT Numero, Freguesia, ConcelhoId FROM $QualifiedTableName"
    $reader = $cmd.ExecuteReader()

    $map = @{}
    while ($reader.Read()) {
        $numero = $reader.GetInt32(0)
        $freguesia = if ($reader.IsDBNull(1)) { "" } else { $reader.GetString(1) }
        $concelhoId = if ($reader.IsDBNull(2)) { -1 } else { $reader.GetInt32(2) }
        $map[$numero] = [pscustomobject]@{
            Numero = $numero
            Freguesia = $freguesia
            ConcelhoId = $concelhoId
        }
    }
    $reader.Close()

    return $map
}

function Read-CsvRows {
    param(
        [string]$Path
    )

    $encoding = [System.Text.Encoding]::GetEncoding(1252)
    $lines = [System.IO.File]::ReadAllLines($Path, $encoding)
    if ($null -eq $lines -or $lines.Length -eq 0) {
        return @()
    }

    $headers = $lines[0].Split(",")
    if ($null -eq $headers -or $headers.Count -eq 0) {
        return @()
    }

    $rows = New-Object System.Collections.Generic.List[object]
    for ($lineIndex = 1; $lineIndex -lt $lines.Length; $lineIndex++) {
        $line = [string]$lines[$lineIndex]
        if ([string]::IsNullOrWhiteSpace($line)) { continue }

        # Some source lines contain malformed quotes in the final column.
        # Split only up to header count so commas in the last field are preserved.
        $fields = $line.Split(",", $headers.Count)
        if ($null -eq $fields -or $fields.Count -eq 0) { continue }

        $obj = [ordered]@{}
        for ($i = 0; $i -lt $headers.Count; $i++) {
            $name = [string]$headers[$i]
            $value = if ($i -lt $fields.Count) { [string]$fields[$i] } else { "" }
            if ($null -eq $value) { $value = "" }
            $value = $value.Trim().Trim('"')
            $obj[$name] = $value
        }

        $obj["__CsvLineNumber"] = $lineIndex + 1

        $rows.Add([pscustomobject]$obj)
    }

    return $rows
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

$resolvedCsvPath = if ([System.IO.Path]::IsPathRooted($CsvPath)) {
    $CsvPath
} else {
    Join-Path $repoRoot $CsvPath
}

if (-not (Test-Path $resolvedCsvPath)) {
    $fallbackCsvPath = Join-Path $repoRoot "codigopostal.csv"
    if (Test-Path $fallbackCsvPath) {
        $resolvedCsvPath = $fallbackCsvPath
    } else {
        throw "CSV nao encontrado em: $resolvedCsvPath"
    }
}

Log-Info "CSV de origem: $resolvedCsvPath"

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = Get-ConnectionStringFromAppSettings -RepoRoot $repoRoot
    Log-Info "Connection string lida de appsettings.json"
} else {
    Log-Info "Connection string recebida por parametro"
}

if ($CompareOnly) {
    if ([string]::IsNullOrWhiteSpace($SourceConnectionString)) {
        $SourceConnectionString = Get-ConnectionStringFromAppSettings -RepoRoot $repoRoot
    }

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        $ConnectionString = $SourceConnectionString
    }

    $qualifiedSourceTable = Get-SafeQualifiedTableName -TableName $SourceTable
    $qualifiedTargetTable = Get-SafeQualifiedTableName -TableName $TargetTable

    Log-Info "Modo CompareOnly ativo."
    Log-Info "Origem: $qualifiedSourceTable"
    Log-Info "Destino: $qualifiedTargetTable"

    $sourceConnection = [System.Data.SqlClient.SqlConnection]::new($SourceConnectionString)
    $targetConnection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)

    try {
        Log-Info "A abrir ligacao SQL origem..."
        $sourceConnection.Open()
        Log-Info "A abrir ligacao SQL destino..."
        $targetConnection.Open()

        $sourceMap = Read-CodigosPostaisTable -Connection $sourceConnection -QualifiedTableName $qualifiedSourceTable
        $targetMap = Read-CodigosPostaisTable -Connection $targetConnection -QualifiedTableName $qualifiedTargetTable

        Log-Info "Registos origem: $($sourceMap.Count)"
        Log-Info "Registos destino: $($targetMap.Count)"

        $missingInTarget = New-Object System.Collections.Generic.List[object]
        $differentValues = New-Object System.Collections.Generic.List[object]

        foreach ($numero in $sourceMap.Keys) {
            if (-not $targetMap.ContainsKey($numero)) {
                $missingInTarget.Add($sourceMap[$numero])
                continue
            }

            $src = $sourceMap[$numero]
            $dst = $targetMap[$numero]

            if ($src.Freguesia -ne $dst.Freguesia -or $src.ConcelhoId -ne $dst.ConcelhoId) {
                $differentValues.Add([pscustomobject]@{
                    Numero = $numero
                    SourceFreguesia = $src.Freguesia
                    TargetFreguesia = $dst.Freguesia
                    SourceConcelhoId = $src.ConcelhoId
                    TargetConcelhoId = $dst.ConcelhoId
                })
            }
        }

        Write-Host "Resumo CompareOnly:"
        Write-Host " - Em falta no destino: $($missingInTarget.Count)"
        Write-Host " - Com valores diferentes: $($differentValues.Count)"

        if ($missingInTarget.Count -gt 0) {
            Write-Host ""
            Log-Warn "Top 200 registos em falta no destino:"
            $missingInTarget | Select-Object -First 200 | ForEach-Object {
                Write-Host (" - Numero={0}, Freguesia='{1}', ConcelhoId={2}" -f $_.Numero, $_.Freguesia, $_.ConcelhoId)
            }
        }

        if ($differentValues.Count -gt 0) {
            Write-Host ""
            Log-Warn "Top 200 registos com diferencas:"
            $differentValues | Select-Object -First 200 | ForEach-Object {
                Write-Host (" - Numero={0}, Origem(Freguesia='{1}', ConcelhoId={2}) vs Destino(Freguesia='{3}', ConcelhoId={4})" -f $_.Numero, $_.SourceFreguesia, $_.SourceConcelhoId, $_.TargetFreguesia, $_.TargetConcelhoId)
            }
        }
    }
    finally {
        if ($sourceConnection.State -eq [System.Data.ConnectionState]::Open) {
            $sourceConnection.Close()
        }
        if ($targetConnection.State -eq [System.Data.ConnectionState]::Open) {
            $targetConnection.Close()
        }
        $sourceConnection.Dispose()
        $targetConnection.Dispose()
    }

    exit 0
}

$rows = Read-CsvRows -Path $resolvedCsvPath
if ($rows.Count -eq 0) {
    Log-Warn "CSV vazio. Nada a importar."
    exit 0
}
Log-Info "Linhas lidas do CSV: $($rows.Count)"
if ($DryRun) {
    Log-Warn "Modo DryRun ativo: apenas as primeiras 10 linhas serao processadas."
}

$connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
Log-Info "A abrir ligacao SQL..."
$connection.Open()
Log-Info "Ligacao SQL aberta."

$inserted = 0
$updated = 0
$skipped = 0
$missingConcelhosByNormalized = @{}
$processed = 0
$skippedByReason = @{
    "NumeroInvalido" = 0
    "NumeroForaIntervalo" = 0
    "NumeroNaoComecaPor9" = 0
    "CamposObrigatoriosVazios" = 0
    "ConcelhoNaoEncontrado" = 0
    "CodigoPostalJaExiste" = 0
    "DbCheckConstraintNumeroRange" = 0
}
$distinctErrors = New-Object System.Collections.Generic.HashSet[string]
$emptyFieldErrors = New-Object System.Collections.Generic.List[object]

try {
    $concelhosCmd = $connection.CreateCommand()
    $concelhosCmd.CommandText = "SELECT Id, Concelho FROM Concelhos"
    $reader = $concelhosCmd.ExecuteReader()

    $concelhoByNormalizedName = @{}
    while ($reader.Read()) {
        $id = $reader.GetInt32(0)
        $name = $reader.GetString(1)
        $normalized = Normalize-Text $name
        if (-not $concelhoByNormalizedName.ContainsKey($normalized)) {
            $concelhoByNormalizedName[$normalized] = $id
        }
    }
    $reader.Close()
    Log-Info "Concelhos carregados da base de dados: $($concelhoByNormalizedName.Count)"

    $rowsToProcess = if ($DryRun) { [Math]::Min(10, $rows.Count) } else { $rows.Count }

    foreach ($row in $rows) {
        if ($processed -ge $rowsToProcess) {
            break
        }

        $processed++
        $numeroRaw = [string]$row.cod_postal
        $concelhoRaw = [string]$row.Concelho
        if ([string]::IsNullOrWhiteSpace($concelhoRaw) -and $row.PSObject.Properties.Name -contains "Concelho_map") {
            $concelhoRaw = [string]$row.Concelho_map
        }

        $freguesiaFinal = ""
        if ($row.PSObject.Properties.Name -contains "Freguesia Final") {
            $freguesiaFinal = [string]$row."Freguesia Final"
        }

        $freguesia = $freguesiaFinal.Trim()
        if ($freguesia.Length -ge 2 -and $freguesia.StartsWith('"') -and $freguesia.EndsWith('"')) {
            $freguesia = $freguesia.Substring(1, $freguesia.Length - 2)
        }
        $concelhoName = $concelhoRaw.Trim()

        $numero = 0
        if (-not [int]::TryParse($numeroRaw, [ref]$numero)) {
            $skipped++
            $skippedByReason["NumeroInvalido"]++
            [void]$distinctErrors.Add("NumeroInvalido")
            Log-Debug "Linha $processed ignorada: cod_postal invalido ('$numeroRaw')."
            continue
        }

        if ($numero -lt 1000000 -or $numero -gt 9999999) {
            $skipped++
            $skippedByReason["NumeroForaIntervalo"]++
            [void]$distinctErrors.Add("NumeroForaIntervalo")
            Log-Warn "Linha $processed ignorada: numero fora do intervalo [1000000..9999999] (valor: $numero)."
            continue
        }

        if ($OnlyStartWith9 -and -not $numeroRaw.Trim().StartsWith("9")) {
            $skipped++
            $skippedByReason["NumeroNaoComecaPor9"]++
            [void]$distinctErrors.Add("NumeroNaoComecaPor9")
            Log-Debug "Linha $processed ignorada: cod_postal nao comeca por 9 ('$numeroRaw')."
            continue
        }

        if ([string]::IsNullOrWhiteSpace($freguesia) -or [string]::IsNullOrWhiteSpace($concelhoName)) {
            $skipped++
            $skippedByReason["CamposObrigatoriosVazios"]++
            [void]$distinctErrors.Add("CamposObrigatoriosVazios")
            $csvLineNumber = if ($row.PSObject.Properties.Name -contains "__CsvLineNumber") { [int]$row.__CsvLineNumber } else { -1 }
            $emptyFieldErrors.Add([pscustomobject]@{
                CsvLineNumber = $csvLineNumber
                CodPostal = $numeroRaw
                Freguesia = $freguesia
                Concelho = $concelhoName
            })
            Log-Debug "Linha $processed ignorada: freguesia ou concelho vazio."
            continue
        }

        $normalizedConcelho = Normalize-Text $concelhoName
        if (-not $concelhoByNormalizedName.ContainsKey($normalizedConcelho)) {
            if (-not $missingConcelhosByNormalized.ContainsKey($normalizedConcelho)) {
                $missingConcelhosByNormalized[$normalizedConcelho] = $concelhoName
            }
            $skipped++
            $skippedByReason["ConcelhoNaoEncontrado"]++
            [void]$distinctErrors.Add("ConcelhoNaoEncontrado")
            Log-Warn "Linha $processed ignorada: concelho nao encontrado na base de dados (cod_postal '$numeroRaw', concelho CSV '$concelhoName')."
            continue
        }

        $concelhoId = [int]$concelhoByNormalizedName[$normalizedConcelho]

        try {
            $checkCmd = $connection.CreateCommand()
            $checkCmd.CommandText = "SELECT TOP 1 Numero FROM CodigosPostais WHERE Numero = @Numero"
            [void]$checkCmd.Parameters.Add("@Numero", [System.Data.SqlDbType]::Int)
            $checkCmd.Parameters["@Numero"].Value = $numero
            $existingNumero = $checkCmd.ExecuteScalar()

            if ($null -eq $existingNumero) {
                $insertCmd = $connection.CreateCommand()
                $insertCmd.CommandText = @"
INSERT INTO CodigosPostais (Numero, Freguesia, ConcelhoId)
VALUES (@Numero, @Freguesia, @ConcelhoId)
"@
                [void]$insertCmd.Parameters.Add("@Numero", [System.Data.SqlDbType]::Int)
                [void]$insertCmd.Parameters.Add("@Freguesia", [System.Data.SqlDbType]::NVarChar, 200)
                [void]$insertCmd.Parameters.Add("@ConcelhoId", [System.Data.SqlDbType]::Int)
                $insertCmd.Parameters["@Numero"].Value = $numero
                $insertCmd.Parameters["@Freguesia"].Value = $freguesia
                $insertCmd.Parameters["@ConcelhoId"].Value = $concelhoId
                [void]$insertCmd.ExecuteNonQuery()
                $inserted++
                Log-Debug "Linha ${processed}: INSERT Numero=$numero, Freguesia='$freguesia', ConcelhoId=$concelhoId."
            } else {
                $skipped++
                $skippedByReason["CodigoPostalJaExiste"]++
                Log-Debug "Linha ${processed}: IGNORADA Numero=$numero (codigo postal ja existe)."
            }
        }
        catch {
            $errorText = [string]$_.Exception.Message
            if ($errorText -like "*CK_CodigosPostais_Numero_Range*") {
                $skipped++
                $skippedByReason["DbCheckConstraintNumeroRange"]++
                [void]$distinctErrors.Add("DbCheckConstraintNumeroRange")
                Log-Warn "Linha $processed ignorada: falha na constraint CK_CodigosPostais_Numero_Range (cod_postal '$numeroRaw')."
                continue
            }
            throw
        }

        if (($processed % 1000) -eq 0) {
            Log-Info "Progresso: $processed/$rowsToProcess linhas processadas..."
        }
    }

    Log-Info "Importacao concluida."
    Write-Host "Resumo:"
    Write-Host " - Processados: $processed"
    Write-Host " - Total de linhas consideradas para esta execucao: $rowsToProcess"
    Write-Host " - Inseridos: $inserted"
    Write-Host " - Atualizados: $updated"
    Write-Host " - Ignorados: $skipped"
    Write-Host " - Ignorados por motivo:"
    Write-Host "   * Numero invalido: $($skippedByReason["NumeroInvalido"])"
    Write-Host "   * Numero fora do intervalo [1000000..9999999]: $($skippedByReason["NumeroForaIntervalo"])"
    Write-Host "   * Numero nao comeca por 9: $($skippedByReason["NumeroNaoComecaPor9"])"
    Write-Host "   * Freguesia/Concelho vazio: $($skippedByReason["CamposObrigatoriosVazios"])"
    Write-Host "   * Concelho nao encontrado: $($skippedByReason["ConcelhoNaoEncontrado"])"
    Write-Host "   * Codigo postal ja existe: $($skippedByReason["CodigoPostalJaExiste"])"
    Write-Host "   * Erro DB (CK_CodigosPostais_Numero_Range): $($skippedByReason["DbCheckConstraintNumeroRange"])"

    if ($missingConcelhosByNormalized.Count -gt 0) {
        $missingUnique = $missingConcelhosByNormalized.Values | Sort-Object
        Write-Host ""
        Log-Warn "Concelhos nao encontrados (lista unica):"
        $missingUnique | ForEach-Object { Write-Host " - $_" }
    }

    if ($distinctErrors.Count -gt 0) {
        Write-Host ""
        Log-Warn "Resumo de erros distintos encontrados:"
        $distinctErrors | Sort-Object | ForEach-Object { Write-Host " - $_" }
    }

    if ($emptyFieldErrors.Count -gt 0) {
        Write-Host ""
        Log-Warn "Top 100 erros de 'Freguesia/Concelho vazio' (com linha no CSV):"
        $emptyFieldErrors |
            Select-Object -First 100 |
            ForEach-Object {
                Write-Host (" - CSV linha {0}: cod_postal='{1}', freguesia='{2}', concelho='{3}'" -f $_.CsvLineNumber, $_.CodPostal, $_.Freguesia, $_.Concelho)
            }
    }

}
catch {
    throw
}
finally {
    Log-Info "A fechar ligacao SQL..."
    $connection.Close()
    $connection.Dispose()
    Log-Info "Ligacao SQL fechada."
}
