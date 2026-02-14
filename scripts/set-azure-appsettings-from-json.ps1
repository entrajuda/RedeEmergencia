param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$WebAppName,

    [string]$AppSettingsPath = "src/REA.Emergencia.Web/appsettings.json",

    [switch]$UseTemplateIfMissing,

    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Get-ResolvedSettingsPath {
    param(
        [string]$Path,
        [bool]$AllowTemplateFallback
    )

    if (Test-Path $Path) {
        return (Resolve-Path $Path).Path
    }

    if ($AllowTemplateFallback) {
        $templatePath = [System.IO.Path]::ChangeExtension($Path, ".template.json")
        if (Test-Path $templatePath) {
            return (Resolve-Path $templatePath).Path
        }
    }

    throw "Settings file not found: $Path"
}

function ConvertTo-AzureAppSettings {
    param(
        [object]$Value,
        [string]$Prefix = ""
    )

    $settings = New-Object 'System.Collections.Generic.Dictionary[string,string]'

    if ($null -ne $Value -and
        $Value -isnot [string] -and
        $Value -isnot [System.Collections.IDictionary] -and
        $Value.PSObject -and
        $Value.PSObject.Properties.Count -gt 0) {

        foreach ($prop in $Value.PSObject.Properties) {
            $childPrefix = if ([string]::IsNullOrWhiteSpace($Prefix)) {
                [string]$prop.Name
            } else {
                "$Prefix`__$($prop.Name)"
            }

            $childSettings = ConvertTo-AzureAppSettings -Value $prop.Value -Prefix $childPrefix
            foreach ($childKey in $childSettings.Keys) {
                $settings[$childKey] = $childSettings[$childKey]
            }
        }

        return $settings
    }

    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            $childPrefix = if ([string]::IsNullOrWhiteSpace($Prefix)) {
                [string]$key
            } else {
                "$Prefix`__$key"
            }

            $childSettings = ConvertTo-AzureAppSettings -Value $Value[$key] -Prefix $childPrefix
            foreach ($childKey in $childSettings.Keys) {
                $settings[$childKey] = $childSettings[$childKey]
            }
        }

        return $settings
    }

    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        $index = 0
        foreach ($item in $Value) {
            $childPrefix = "$Prefix`__${index}"
            $childSettings = ConvertTo-AzureAppSettings -Value $item -Prefix $childPrefix
            foreach ($childKey in $childSettings.Keys) {
                $settings[$childKey] = $childSettings[$childKey]
            }
            $index++
        }
        return $settings
    }

    $stringValue = if ($null -eq $Value) { "" } else { [string]$Value }
    $settings[$Prefix] = $stringValue
    return $settings
}

function Ensure-AzCli {
    $azCmd = Get-Command az -ErrorAction SilentlyContinue
    if ($null -eq $azCmd) {
        throw "Azure CLI (az) was not found in PATH. Install it first."
    }
}

function Invoke-SetAzureSettings {
    param(
        [string]$Rg,
        [string]$App,
        [System.Collections.Generic.Dictionary[string,string]]$SettingsMap,
        [bool]$DryRun
    )

    $pairs = New-Object 'System.Collections.Generic.List[string]'
    $orderedMap = [ordered]@{}
    foreach ($key in $SettingsMap.Keys | Sort-Object) {
        $value = $SettingsMap[$key]
        $pairs.Add("$key=$value")
        $orderedMap[$key] = $value
    }

    if ($DryRun) {
        Write-Host "[WhatIf] Would set $($pairs.Count) settings on app '$App' in resource group '$Rg':"
        $pairs | ForEach-Object { Write-Host " - $_" }
        return
    }

    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
        $json = $orderedMap | ConvertTo-Json -Depth 10
        Set-Content -Path $tempFile -Value $json -Encoding UTF8

        Write-Host "Applying $($pairs.Count) settings..."
        $settingsFileArg = "@$tempFile"
        az webapp config appsettings set `
            --resource-group $Rg `
            --name $App `
            --settings $settingsFileArg `
            --output table | Out-Host

        if ($LASTEXITCODE -ne 0) {
            throw "Azure CLI failed while applying app settings (exit code: $LASTEXITCODE)."
        }
    }
    finally {
        if (Test-Path $tempFile) {
            Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
        }
    }
}

Ensure-AzCli

$resolvedPath = Get-ResolvedSettingsPath -Path $AppSettingsPath -AllowTemplateFallback:$UseTemplateIfMissing
Write-Host "Reading app settings from: $resolvedPath"

$json = Get-Content -Raw -Path $resolvedPath | ConvertFrom-Json
$settingsMap = ConvertTo-AzureAppSettings -Value $json

if ($settingsMap.Count -eq 0) {
    throw "No settings found in $resolvedPath."
}

Invoke-SetAzureSettings `
    -Rg $ResourceGroup `
    -App $WebAppName `
    -SettingsMap $settingsMap `
    -DryRun:$WhatIf

Write-Host "Done."
