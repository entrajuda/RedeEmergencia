param(
    [string]$ProjectPath = "src/REA.Emergencia.Web",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$projectFullPath = Resolve-Path $ProjectPath
$templatePairs = @(
    @{
        Source = Join-Path $projectFullPath "appsettings.template.json"
        Target = Join-Path $projectFullPath "appsettings.json"
    },
    @{
        Source = Join-Path $projectFullPath "appsettings.Development.template.json"
        Target = Join-Path $projectFullPath "appsettings.Development.json"
    }
)

foreach ($pair in $templatePairs) {
    if (-not (Test-Path $pair.Source)) {
        throw "Template not found: $($pair.Source)"
    }

    if ((Test-Path $pair.Target) -and -not $Force) {
        Write-Host "Skipping existing file: $($pair.Target)"
        continue
    }

    Copy-Item -Path $pair.Source -Destination $pair.Target -Force
    Write-Host "Created: $($pair.Target)"
}

Write-Host "Done. Update the generated appsettings values for your environment."
