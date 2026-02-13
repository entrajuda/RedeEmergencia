param(
    [string]$CsvPath = ".\codigopostal.csv",
    [string]$ConnectionString = "",
    [switch]$DetailedLog,
    [switch]$DryRun
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
$transaction = $connection.BeginTransaction()

$inserted = 0
$updated = 0
$skipped = 0
$missingConcelhosByNormalized = @{}
$processed = 0
$skippedByReason = @{
    "NumeroInvalido" = 0
    "NumeroForaIntervalo" = 0
    "CamposObrigatoriosVazios" = 0
    "ConcelhoNaoEncontrado" = 0
    "DbCheckConstraintNumeroRange" = 0
}
$distinctErrors = New-Object System.Collections.Generic.HashSet[string]
$emptyFieldErrors = New-Object System.Collections.Generic.List[object]

try {
    $concelhosCmd = $connection.CreateCommand()
    $concelhosCmd.Transaction = $transaction
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
            $checkCmd.Transaction = $transaction
            $checkCmd.CommandText = "SELECT TOP 1 Numero FROM CodigosPostais WHERE Numero = @Numero"
            [void]$checkCmd.Parameters.Add("@Numero", [System.Data.SqlDbType]::Int)
            $checkCmd.Parameters["@Numero"].Value = $numero
            $existingNumero = $checkCmd.ExecuteScalar()

            if ($null -eq $existingNumero) {
                $insertCmd = $connection.CreateCommand()
                $insertCmd.Transaction = $transaction
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
                $updateCmd = $connection.CreateCommand()
                $updateCmd.Transaction = $transaction
                $updateCmd.CommandText = @"
UPDATE CodigosPostais
SET Freguesia = @Freguesia,
    ConcelhoId = @ConcelhoId
WHERE Numero = @Numero
"@
                [void]$updateCmd.Parameters.Add("@Numero", [System.Data.SqlDbType]::Int)
                [void]$updateCmd.Parameters.Add("@Freguesia", [System.Data.SqlDbType]::NVarChar, 200)
                [void]$updateCmd.Parameters.Add("@ConcelhoId", [System.Data.SqlDbType]::Int)
                $updateCmd.Parameters["@Numero"].Value = $numero
                $updateCmd.Parameters["@Freguesia"].Value = $freguesia
                $updateCmd.Parameters["@ConcelhoId"].Value = $concelhoId
                [void]$updateCmd.ExecuteNonQuery()
                $updated++
                Log-Debug "Linha ${processed}: UPDATE Numero=$numero, Freguesia='$freguesia', ConcelhoId=$concelhoId."
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

    $transaction.Commit()

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
    Write-Host "   * Freguesia/Concelho vazio: $($skippedByReason["CamposObrigatoriosVazios"])"
    Write-Host "   * Concelho nao encontrado: $($skippedByReason["ConcelhoNaoEncontrado"])"
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
    try {
        $transaction.Rollback()
        Log-Warn "Transacao revertida devido a erro."
    }
    catch {
    }
    throw
}
finally {
    Log-Info "A fechar ligacao SQL..."
    $connection.Close()
    $connection.Dispose()
    Log-Info "Ligacao SQL fechada."
}
