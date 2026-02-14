param(
    [Parameter(Mandatory = $true)]
    [string]$TargetConnectionString,

    [string]$SourceConnectionString = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Data.DataSetExtensions

function Log-Info([string]$msg) { Write-Host "[INFO ] $msg" }
function Log-Warn([string]$msg) { Write-Host "[WARN ] $msg" -ForegroundColor Yellow }

function Get-SourceConnectionStringFromAppSettings {
    $root = Split-Path -Parent $PSScriptRoot
    $appSettingsPath = Join-Path $root "src\REA.Emergencia.Web\appsettings.json"
    if (-not (Test-Path $appSettingsPath)) {
        throw "Nao foi possivel encontrar appsettings.json em '$appSettingsPath'. Passe -SourceConnectionString."
    }

    $json = Get-Content -Raw -Path $appSettingsPath | ConvertFrom-Json
    $conn = [string]$json.ConnectionStrings.DefaultConnection
    if ([string]::IsNullOrWhiteSpace($conn)) {
        throw "ConnectionStrings:DefaultConnection nao encontrado em appsettings.json."
    }

    return $conn
}

function Get-DataTable {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql
    )

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $table = New-Object System.Data.DataTable
    [void]$adapter.Fill($table)
    Write-Output -NoEnumerate $table
}

function Get-FirstDataTable {
    param([object]$Value)
    if ($Value -is [System.Data.DataTable]) {
        return $Value
    }

    if ($Value -is [System.Data.DataRow]) {
        return $Value.Table
    }

    if ($Value -is [System.Array]) {
        foreach ($item in $Value) {
            if ($item -is [System.Data.DataTable]) {
                return $item
            }
            if ($item -is [System.Data.DataRow]) {
                return $item.Table
            }
        }
    }

    throw "Nao foi possivel obter um DataTable a partir do resultado."
}

function Get-DataRows {
    param([object]$Value)

    $rows = New-Object System.Collections.Generic.List[System.Data.DataRow]

    if ($Value -is [System.Data.DataTable]) {
        foreach ($r in $Value.Select()) { [void]$rows.Add($r) }
        return $rows
    }

    if ($Value -is [System.Data.DataRow]) {
        [void]$rows.Add($Value)
        return $rows
    }

    if ($Value -is [System.Array]) {
        foreach ($item in $Value) {
            if ($item -is [System.Data.DataRow]) {
                [void]$rows.Add($item)
                continue
            }
            if ($item -is [System.Data.DataTable]) {
                foreach ($r in $item.Select()) { [void]$rows.Add($r) }
            }
        }
        return $rows
    }

    throw "Nao foi possivel obter DataRows a partir do resultado."
}

function Execute-Scalar {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [System.Data.SqlClient.SqlTransaction]$Transaction,
        [string]$Sql,
        [hashtable]$Parameters = @{}
    )

    $cmd = $Connection.CreateCommand()
    $cmd.Transaction = $Transaction
    $cmd.CommandText = $Sql
    foreach ($k in $Parameters.Keys) {
        $paramValue = $Parameters[$k]
        if ($null -eq $paramValue) {
            $paramValue = [DBNull]::Value
        }
        [void]$cmd.Parameters.AddWithValue($k, $paramValue)
    }
    return $cmd.ExecuteScalar()
}

function Execute-NonQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [System.Data.SqlClient.SqlTransaction]$Transaction,
        [string]$Sql,
        [hashtable]$Parameters = @{}
    )

    $cmd = $Connection.CreateCommand()
    $cmd.Transaction = $Transaction
    $cmd.CommandText = $Sql
    foreach ($k in $Parameters.Keys) {
        $paramValue = $Parameters[$k]
        if ($null -eq $paramValue) {
            $paramValue = [DBNull]::Value
        }
        [void]$cmd.Parameters.AddWithValue($k, $paramValue)
    }
    return $cmd.ExecuteNonQuery()
}

function Get-TableCount {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$TableName
    )

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(1) FROM dbo.$TableName"
    return [int]$cmd.ExecuteScalar()
}

if ([string]::IsNullOrWhiteSpace($SourceConnectionString)) {
    $SourceConnectionString = Get-SourceConnectionStringFromAppSettings
}

Log-Info "A abrir ligacao origem..."
$source = [System.Data.SqlClient.SqlConnection]::new($SourceConnectionString)
$source.Open()
Log-Info "A abrir ligacao destino..."
$target = [System.Data.SqlClient.SqlConnection]::new($TargetConnectionString)
$target.Open()

Log-Info "Origem: $($source.DataSource) / DB: $($source.Database)"
Log-Info "Destino: $($target.DataSource) / DB: $($target.Database)"
if ($source.DataSource -eq $target.DataSource -and $source.Database -eq $target.Database) {
    Log-Warn "Origem e destino parecem ser a mesma base de dados."
}
Log-Info "Destino (SQL): Server=$([string](Execute-Scalar -Connection $target -Transaction $null -Sql 'SELECT @@SERVERNAME')) | DB=$([string](Execute-Scalar -Connection $target -Transaction $null -Sql 'SELECT DB_NAME()'))"

try {
    Log-Info "A ler dados da origem..."
    $srcDistritosRows = Get-DataRows -Value (Get-DataTable -Connection $source -Sql "SELECT Id, Distrito FROM Distritos ORDER BY Id")
    $srcConcelhosRows = Get-DataRows -Value (Get-DataTable -Connection $source -Sql "SELECT Id, Concelho, DistritoId, ZINF FROM Concelhos ORDER BY Id")
    $srcTiposPedidoRows = Get-DataRows -Value (Get-DataTable -Connection $source -Sql "SELECT Id, Name, CreatedAtUtc, Workflow, TableName FROM TiposPedido ORDER BY Id")
    $srcCodigosPostaisRows = Get-DataRows -Value (Get-DataTable -Connection $source -Sql "SELECT Numero, Freguesia, ConcelhoId FROM CodigosPostais ORDER BY Numero")

    Log-Info "Distritos origem: $($srcDistritosRows.Count)"
    Log-Info "Concelhos origem: $($srcConcelhosRows.Count)"
    Log-Info "TiposPedido origem: $($srcTiposPedidoRows.Count)"
    Log-Info "CodigosPostais origem: $($srcCodigosPostaisRows.Count)"

    # 1) Distritos (pai)
    $distritoMap = @{} # sourceId -> targetId
    $insDistritos = 0
    $updDistritos = 0
    $txDistritos = $target.BeginTransaction()
    try {
        Log-Info "Distritos a processar: $($srcDistritosRows.Count)"
        foreach ($row in $srcDistritosRows) {
            $sourceDistritoId = [int]$row["Id"]
            $nome = [string]$row["Distrito"]

            $targetDistritoId = Execute-Scalar -Connection $target -Transaction $txDistritos -Sql @"
SELECT TOP 1 Id
FROM dbo.Distritos
WHERE Distrito = @Nome
"@ -Parameters @{ "@Nome" = $nome }

            if ($null -eq $targetDistritoId) {
                $targetDistritoId = Execute-Scalar -Connection $target -Transaction $txDistritos -Sql @"
INSERT INTO dbo.Distritos (Distrito)
OUTPUT INSERTED.Id
VALUES (@Nome)
"@ -Parameters @{ "@Nome" = $nome }
                $insDistritos++
            } else {
                $updDistritos++
            }

            $distritoMap[$sourceDistritoId] = [int]$targetDistritoId
        }
        $txDistritos.Commit()
        Log-Info "Tabela Distritos concluida (commit efetuado)."
        Log-Info "Destino dbo.Distritos total: $(Get-TableCount -Connection $target -TableName 'Distritos')"
    }
    catch {
        try { $txDistritos.Rollback() } catch {}
        throw
    }

    # 2) Concelhos (depende de Distrito)
    $concelhoMap = @{} # sourceId -> targetId
    $insConcelhos = 0
    $updConcelhos = 0
    $txConcelhos = $target.BeginTransaction()
    try {
        Log-Info "Concelhos a processar: $($srcConcelhosRows.Count)"
        foreach ($row in $srcConcelhosRows) {
            $sourceConcelhoId = [int]$row["Id"]
            $nome = [string]$row["Concelho"]
            $sourceDistritoId = [int]$row["DistritoId"]
            $zinf = [string]$row["ZINF"]

            if (-not $distritoMap.ContainsKey($sourceDistritoId)) {
                throw "DistritoId '$sourceDistritoId' nao encontrado no mapa ao copiar concelhos."
            }
            $targetDistritoId = [int]$distritoMap[$sourceDistritoId]

            $targetConcelhoId = Execute-Scalar -Connection $target -Transaction $txConcelhos -Sql @"
SELECT TOP 1 Id
FROM dbo.Concelhos
WHERE Concelho = @Nome
"@ -Parameters @{ "@Nome" = $nome }

            if ($null -eq $targetConcelhoId) {
                $targetConcelhoId = Execute-Scalar -Connection $target -Transaction $txConcelhos -Sql @"
INSERT INTO dbo.Concelhos (Concelho, DistritoId, ZINF)
OUTPUT INSERTED.Id
VALUES (@Nome, @DistritoId, @ZINF)
"@ -Parameters @{
                    "@Nome" = $nome
                    "@DistritoId" = $targetDistritoId
                    "@ZINF" = $zinf
                }
                $insConcelhos++
            } else {
                [void](Execute-NonQuery -Connection $target -Transaction $txConcelhos -Sql @"
UPDATE dbo.Concelhos
SET DistritoId = @DistritoId,
    ZINF = @ZINF
WHERE Id = @Id
"@ -Parameters @{
                        "@Id" = [int]$targetConcelhoId
                        "@DistritoId" = $targetDistritoId
                        "@ZINF" = $zinf
                    })
                $updConcelhos++
            }

            $concelhoMap[$sourceConcelhoId] = [int]$targetConcelhoId
        }
        $txConcelhos.Commit()
        Log-Info "Tabela Concelhos concluida (commit efetuado)."
        Log-Info "Destino dbo.Concelhos total: $(Get-TableCount -Connection $target -TableName 'Concelhos')"
    }
    catch {
        try { $txConcelhos.Rollback() } catch {}
        throw
    }

    # 3) TiposPedido (independente)
    $insTipos = 0
    $updTipos = 0
    $txTipos = $target.BeginTransaction()
    try {
        Log-Info "TiposPedido a processar: $($srcTiposPedidoRows.Count)"
        foreach ($row in $srcTiposPedidoRows) {
            $name = [string]$row["Name"]
            $createdAtUtc = [datetime]$row["CreatedAtUtc"]
            $workflow = [string]$row["Workflow"]
            $tableName = [string]$row["TableName"]

            $targetTipoPedidoId = Execute-Scalar -Connection $target -Transaction $txTipos -Sql @"
SELECT TOP 1 Id
FROM dbo.TiposPedido
WHERE TableName = @TableName
"@ -Parameters @{ "@TableName" = $tableName }

            if ($null -eq $targetTipoPedidoId) {
                [void](Execute-NonQuery -Connection $target -Transaction $txTipos -Sql @"
INSERT INTO dbo.TiposPedido (Name, CreatedAtUtc, Workflow, TableName)
VALUES (@Name, @CreatedAtUtc, @Workflow, @TableName)
"@ -Parameters @{
                        "@Name" = $name
                        "@CreatedAtUtc" = $createdAtUtc
                        "@Workflow" = $workflow
                        "@TableName" = $tableName
                    })
                $insTipos++
            } else {
                [void](Execute-NonQuery -Connection $target -Transaction $txTipos -Sql @"
UPDATE dbo.TiposPedido
SET Name = @Name,
    Workflow = @Workflow
WHERE Id = @Id
"@ -Parameters @{
                        "@Id" = [int]$targetTipoPedidoId
                        "@Name" = $name
                        "@Workflow" = $workflow
                    })
                $updTipos++
            }
        }
        $txTipos.Commit()
        Log-Info "Tabela TiposPedido concluida (commit efetuado)."
        Log-Info "Destino dbo.TiposPedido total: $(Get-TableCount -Connection $target -TableName 'TiposPedido')"
    }
    catch {
        try { $txTipos.Rollback() } catch {}
        throw
    }

    # 4) CodigosPostais (depende de Concelhos)
    $insCodigos = 0
    $updCodigos = 0
    $totalCodigos = $srcCodigosPostaisRows.Count
    $txCodigos = $target.BeginTransaction()
    try {
        $i = 0
        Log-Info "CodigosPostais a processar: $totalCodigos"
        foreach ($row in $srcCodigosPostaisRows) {
            $numero = [int]$row["Numero"]
            $freguesia = [string]$row["Freguesia"]
            $sourceConcelhoId = [int]$row["ConcelhoId"]

            if (-not $concelhoMap.ContainsKey($sourceConcelhoId)) {
                throw "ConcelhoId '$sourceConcelhoId' nao encontrado no mapa ao copiar codigos postais."
            }
            $targetConcelhoId = [int]$concelhoMap[$sourceConcelhoId]

            $existing = Execute-Scalar -Connection $target -Transaction $txCodigos -Sql @"
SELECT TOP 1 Numero
FROM dbo.CodigosPostais
WHERE Numero = @Numero
"@ -Parameters @{ "@Numero" = $numero }

            if ($null -eq $existing) {
                [void](Execute-NonQuery -Connection $target -Transaction $txCodigos -Sql @"
INSERT INTO dbo.CodigosPostais (Numero, Freguesia, ConcelhoId)
VALUES (@Numero, @Freguesia, @ConcelhoId)
"@ -Parameters @{
                        "@Numero" = $numero
                        "@Freguesia" = $freguesia
                        "@ConcelhoId" = $targetConcelhoId
                    })
                $insCodigos++
            } else {
                [void](Execute-NonQuery -Connection $target -Transaction $txCodigos -Sql @"
UPDATE dbo.CodigosPostais
SET Freguesia = @Freguesia,
    ConcelhoId = @ConcelhoId
WHERE Numero = @Numero
"@ -Parameters @{
                        "@Numero" = $numero
                        "@Freguesia" = $freguesia
                        "@ConcelhoId" = $targetConcelhoId
                    })
                $updCodigos++
            }

            $i++
            $processed = $i
            if (($processed % 5000) -eq 0 -or $processed -eq $totalCodigos) {
                Log-Info "CodigosPostais processados: $processed / $totalCodigos"
            }
        }
        $txCodigos.Commit()
        Log-Info "Tabela CodigosPostais concluida (commit efetuado)."
        Log-Info "Destino dbo.CodigosPostais total: $(Get-TableCount -Connection $target -TableName 'CodigosPostais')"
    }
    catch {
        try { $txCodigos.Rollback() } catch {}
        throw
    }

    Log-Info "Concluido com sucesso."
    Write-Host "Resumo:"
    Write-Host " - Distritos: inseridos=$insDistritos, atualizados=$updDistritos"
    Write-Host " - Concelhos: inseridos=$insConcelhos, atualizados=$updConcelhos"
    Write-Host " - TiposPedido: inseridos=$insTipos, atualizados=$updTipos"
    Write-Host " - CodigosPostais: inseridos=$insCodigos, atualizados=$updCodigos"
}
catch {
    throw
}
finally {
    if ($source.State -eq "Open") { $source.Close() }
    if ($target.State -eq "Open") { $target.Close() }
    $source.Dispose()
    $target.Dispose()
}
