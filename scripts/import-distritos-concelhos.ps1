param(
    [string]$CsvPath = ".\lista.csv",
    [string]$ConnectionString = ""
)

$ErrorActionPreference = "Stop"

function Get-ConnectionStringFromAppSettings {
    param(
        [string]$RepoRoot
    )

    $appSettingsPath = Join-Path $RepoRoot "src\REA.Emergencia.Web\appsettings.json"
    if (-not (Test-Path $appSettingsPath)) {
        throw "Não foi possível encontrar appsettings.json em $appSettingsPath"
    }

    $json = Get-Content -Path $appSettingsPath -Raw | ConvertFrom-Json
    $conn = $json.ConnectionStrings.DefaultConnection
    if ([string]::IsNullOrWhiteSpace($conn)) {
        throw "ConnectionStrings:DefaultConnection não está definido no appsettings.json."
    }

    return [string]$conn
}

function Normalize-Text {
    param([string]$Value)
    if ($null -eq $Value) { return "" }
    return $Value.Trim()
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

$resolvedCsvPath = if ([System.IO.Path]::IsPathRooted($CsvPath)) {
    $CsvPath
} else {
    Join-Path $repoRoot $CsvPath
}

if (-not (Test-Path $resolvedCsvPath)) {
    $fallbackCsvPath = Join-Path $repoRoot "lista.csv"
    if (Test-Path $fallbackCsvPath) {
        $resolvedCsvPath = $fallbackCsvPath
    } else {
        throw "CSV nao encontrado em: $resolvedCsvPath"
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = Get-ConnectionStringFromAppSettings -RepoRoot $repoRoot
}

$rows = Import-Csv -Path $resolvedCsvPath
if ($rows.Count -eq 0) {
    Write-Host "CSV vazio. Nada a importar."
    exit 0
}

$districtNames = $rows |
    ForEach-Object { Normalize-Text $_.Distrito } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Sort-Object -Unique

$insertedDistricts = 0
$updatedConcelhos = 0
$insertedConcelhos = 0

$connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
$connection.Open()

$transaction = $connection.BeginTransaction()

try {
    $districtIdByName = @{}

    foreach ($districtName in $districtNames) {
        $selectDistrictCmd = $connection.CreateCommand()
        $selectDistrictCmd.Transaction = $transaction
        $selectDistrictCmd.CommandText = "SELECT TOP 1 Id FROM Distritos WHERE Distrito = @Distrito"
        [void]$selectDistrictCmd.Parameters.Add("@Distrito", [System.Data.SqlDbType]::NVarChar, 200)
        $selectDistrictCmd.Parameters["@Distrito"].Value = $districtName
        $existingId = $selectDistrictCmd.ExecuteScalar()

        if ($null -eq $existingId) {
            $insertDistrictCmd = $connection.CreateCommand()
            $insertDistrictCmd.Transaction = $transaction
            $insertDistrictCmd.CommandText = @"
INSERT INTO Distritos (Distrito)
VALUES (@Distrito);
SELECT CAST(SCOPE_IDENTITY() AS int);
"@
            [void]$insertDistrictCmd.Parameters.Add("@Distrito", [System.Data.SqlDbType]::NVarChar, 200)
            $insertDistrictCmd.Parameters["@Distrito"].Value = $districtName
            $existingId = $insertDistrictCmd.ExecuteScalar()
            $insertedDistricts++
        }

        $districtIdByName[$districtName] = [int]$existingId
    }

    foreach ($row in $rows) {
        $districtName = Normalize-Text $row.Distrito
        $concelhoName = Normalize-Text $row.Concelho
        $zinf = Normalize-Text $row.ZINF

        if ([string]::IsNullOrWhiteSpace($districtName) -or [string]::IsNullOrWhiteSpace($concelhoName) -or [string]::IsNullOrWhiteSpace($zinf)) {
            continue
        }

        if (-not $districtIdByName.ContainsKey($districtName)) {
            throw "Distrito '$districtName' não encontrado no mapa durante import de concelhos."
        }

        $districtId = $districtIdByName[$districtName]

        $selectConcelhoCmd = $connection.CreateCommand()
        $selectConcelhoCmd.Transaction = $transaction
        $selectConcelhoCmd.CommandText = @"
SELECT TOP 1 Id, ZINF
FROM Concelhos
WHERE DistritoId = @DistritoId AND Concelho = @Concelho
"@
        [void]$selectConcelhoCmd.Parameters.Add("@DistritoId", [System.Data.SqlDbType]::Int)
        [void]$selectConcelhoCmd.Parameters.Add("@Concelho", [System.Data.SqlDbType]::NVarChar, 200)
        $selectConcelhoCmd.Parameters["@DistritoId"].Value = $districtId
        $selectConcelhoCmd.Parameters["@Concelho"].Value = $concelhoName

        $reader = $selectConcelhoCmd.ExecuteReader()
        $existingConcelhoId = $null
        $existingZinf = $null
        if ($reader.Read()) {
            $existingConcelhoId = $reader.GetInt32(0)
            $existingZinf = $reader.GetString(1)
        }
        $reader.Close()

        if ($null -eq $existingConcelhoId) {
            $insertConcelhoCmd = $connection.CreateCommand()
            $insertConcelhoCmd.Transaction = $transaction
            $insertConcelhoCmd.CommandText = @"
INSERT INTO Concelhos (Concelho, DistritoId, ZINF)
VALUES (@Concelho, @DistritoId, @ZINF);
"@
            [void]$insertConcelhoCmd.Parameters.Add("@Concelho", [System.Data.SqlDbType]::NVarChar, 200)
            [void]$insertConcelhoCmd.Parameters.Add("@DistritoId", [System.Data.SqlDbType]::Int)
            [void]$insertConcelhoCmd.Parameters.Add("@ZINF", [System.Data.SqlDbType]::NVarChar, 100)
            $insertConcelhoCmd.Parameters["@Concelho"].Value = $concelhoName
            $insertConcelhoCmd.Parameters["@DistritoId"].Value = $districtId
            $insertConcelhoCmd.Parameters["@ZINF"].Value = $zinf
            [void]$insertConcelhoCmd.ExecuteNonQuery()
            $insertedConcelhos++
        } elseif ($existingZinf -ne $zinf) {
            $updateConcelhoCmd = $connection.CreateCommand()
            $updateConcelhoCmd.Transaction = $transaction
            $updateConcelhoCmd.CommandText = "UPDATE Concelhos SET ZINF = @ZINF WHERE Id = @Id"
            [void]$updateConcelhoCmd.Parameters.Add("@ZINF", [System.Data.SqlDbType]::NVarChar, 100)
            [void]$updateConcelhoCmd.Parameters.Add("@Id", [System.Data.SqlDbType]::Int)
            $updateConcelhoCmd.Parameters["@ZINF"].Value = $zinf
            $updateConcelhoCmd.Parameters["@Id"].Value = $existingConcelhoId
            [void]$updateConcelhoCmd.ExecuteNonQuery()
            $updatedConcelhos++
        }
    }

    $transaction.Commit()

    Write-Host "Importação concluída."
    Write-Host "Distritos inseridos: $insertedDistricts"
    Write-Host "Concelhos inseridos: $insertedConcelhos"
    Write-Host "Concelhos atualizados (ZINF): $updatedConcelhos"
}
catch {
    try {
        $transaction.Rollback()
    }
    catch {
    }
    throw
}
finally {
    $connection.Close()
    $connection.Dispose()
}
