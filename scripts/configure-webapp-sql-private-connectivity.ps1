param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$WebAppName,

    [Parameter(Mandatory = $true)]
    [string]$SqlServerName,

    [Parameter(Mandatory = $true)]
    [string]$VNetName,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [string]$VNetAddressPrefix = "10.20.0.0/16",

    [string]$IntegrationSubnetName = "appsvc-integration-subnet",
    [string]$IntegrationSubnetPrefix = "10.20.1.0/24",

    [string]$PrivateEndpointSubnetName = "sql-private-endpoint-subnet",
    [string]$PrivateEndpointSubnetPrefix = "10.20.2.0/24",

    [string]$PrivateEndpointName = "",
    [string]$PrivateDnsZoneName = "privatelink.database.windows.net",
    [string]$PrivateDnsZoneLinkName = "",
    [string]$PrivateDnsZoneGroupName = "default",

    [switch]$CreateMissingResources,
    [switch]$DisableSqlPublicNetworkAccess,

    [string]$SqlConnectionString = "",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PrivateEndpointName)) {
    $PrivateEndpointName = "$SqlServerName-pe"
}

if ([string]::IsNullOrWhiteSpace($PrivateDnsZoneLinkName)) {
    $PrivateDnsZoneLinkName = "$VNetName-dns-link"
}

function Log-Info([string]$message) {
    Write-Host "[INFO ] $message"
}

function Log-Warn([string]$message) {
    Write-Host "[WARN ] $message" -ForegroundColor Yellow
}

function Invoke-AzJson {
    param([Parameter(Mandatory = $true)][string[]]$Args)
    $oldNativeErrPref = $null
    $oldErrorActionPreference = $ErrorActionPreference
    $hasNativeErrPref = Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue
    if ($hasNativeErrPref) {
        $oldNativeErrPref = $PSNativeCommandUseErrorActionPreference
        $script:PSNativeCommandUseErrorActionPreference = $false
    }

    try {
        $ErrorActionPreference = "Continue"
        $output = az @Args --only-show-errors --output json 2>$null
        if ($LASTEXITCODE -ne 0) {
            return $null
        }
        if ([string]::IsNullOrWhiteSpace($output)) { return $null }
        return ($output | ConvertFrom-Json)
    }
    finally {
        $ErrorActionPreference = $oldErrorActionPreference
        if ($hasNativeErrPref) {
            $script:PSNativeCommandUseErrorActionPreference = $oldNativeErrPref
        }
    }
}

function Invoke-Az {
    param([Parameter(Mandatory = $true)][string[]]$Args)
    if ($WhatIf) {
        Write-Host "[WhatIf] az $($Args -join ' ')"
        return
    }

    az @Args | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Args -join ' ')"
    }
}

function Ensure-AzCli {
    $azCmd = Get-Command az -ErrorAction SilentlyContinue
    if ($null -eq $azCmd) {
        throw "Azure CLI (az) not found in PATH."
    }
}

function Ensure-LoggedIn {
    $account = Invoke-AzJson -Args @("account", "show")
    if ($null -eq $account) {
        throw "Not logged in to Azure CLI. Run: az login"
    }
    Log-Info "Using subscription: $($account.name) ($($account.id))"
}

function Ensure-ResourceGroup {
    $rg = Invoke-AzJson -Args @("group", "show", "--name", $ResourceGroup)
    if ($null -eq $rg) {
        throw "Resource group '$ResourceGroup' not found."
    }
}

function Get-WebApp {
    return Invoke-AzJson -Args @("webapp", "show", "--resource-group", $ResourceGroup, "--name", $WebAppName)
}

function Get-SqlServer {
    return Invoke-AzJson -Args @("sql", "server", "show", "--resource-group", $ResourceGroup, "--name", $SqlServerName)
}

function Get-VNet {
    return Invoke-AzJson -Args @("network", "vnet", "show", "--resource-group", $ResourceGroup, "--name", $VNetName)
}

function Ensure-VNet {
    $vnet = Get-VNet
    if ($null -ne $vnet) {
        Log-Info "Found VNet '$VNetName'."
        return $vnet
    }

    if (-not $CreateMissingResources) {
        throw "VNet '$VNetName' not found in resource group '$ResourceGroup'. Re-run with -CreateMissingResources or create manually."
    }

    Log-Info "Creating VNet '$VNetName' with address prefix '$VNetAddressPrefix'..."
    Invoke-Az -Args @(
        "network", "vnet", "create",
        "--resource-group", $ResourceGroup,
        "--name", $VNetName,
        "--location", $Location,
        "--address-prefixes", $VNetAddressPrefix
    )

    $vnet = Get-VNet
    if ($null -eq $vnet) {
        throw "Failed to create VNet '$VNetName'."
    }

    return $vnet
}

function Ensure-Subnet {
    param(
        [string]$SubnetName,
        [string]$AddressPrefix,
        [string]$Delegation,
        [bool]$DisablePrivateEndpointNetworkPolicies
    )

    $subnet = Invoke-AzJson -Args @(
        "network", "vnet", "subnet", "show",
        "--resource-group", $ResourceGroup,
        "--vnet-name", $VNetName,
        "--name", $SubnetName
    )

    if ($null -eq $subnet) {
        if (-not $CreateMissingResources) {
            throw "Subnet '$SubnetName' does not exist in VNet '$VNetName'. Re-run with -CreateMissingResources or create manually."
        }

        Log-Info "Creating subnet '$SubnetName'..."
        $args = @(
            "network", "vnet", "subnet", "create",
            "--resource-group", $ResourceGroup,
            "--vnet-name", $VNetName,
            "--name", $SubnetName,
            "--address-prefixes", $AddressPrefix
        )
        if (-not [string]::IsNullOrWhiteSpace($Delegation)) {
            $args += @("--delegations", $Delegation)
        }
        if ($DisablePrivateEndpointNetworkPolicies) {
            $args += @("--disable-private-endpoint-network-policies", "true")
        }
        Invoke-Az -Args $args
        $subnet = Invoke-AzJson -Args @(
            "network", "vnet", "subnet", "show",
            "--resource-group", $ResourceGroup,
            "--vnet-name", $VNetName,
            "--name", $SubnetName
        )
    }
    else {
        Log-Info "Subnet '$SubnetName' already exists."
    }

    if (-not [string]::IsNullOrWhiteSpace($Delegation)) {
        $hasDelegation = $false
        if ($subnet.delegations) {
            foreach ($d in $subnet.delegations) {
                if ($d.serviceName -eq $Delegation) { $hasDelegation = $true; break }
            }
        }

        if (-not $hasDelegation) {
            Log-Info "Applying delegation '$Delegation' to subnet '$SubnetName'..."
            Invoke-Az -Args @(
                "network", "vnet", "subnet", "update",
                "--resource-group", $ResourceGroup,
                "--vnet-name", $VNetName,
                "--name", $SubnetName,
                "--delegations", $Delegation
            )
        }
    }

    if ($DisablePrivateEndpointNetworkPolicies -and $subnet.privateEndpointNetworkPolicies -ne "Disabled") {
        Log-Info "Disabling private endpoint network policies on subnet '$SubnetName'..."
        Invoke-Az -Args @(
            "network", "vnet", "subnet", "update",
            "--resource-group", $ResourceGroup,
            "--vnet-name", $VNetName,
            "--name", $SubnetName,
            "--disable-private-endpoint-network-policies", "true"
        )
    }

    return Invoke-AzJson -Args @(
        "network", "vnet", "subnet", "show",
        "--resource-group", $ResourceGroup,
        "--vnet-name", $VNetName,
        "--name", $SubnetName
    )
}

function Ensure-WebAppVNetIntegration {
    param([string]$SubnetResourceId)

    $integrations = Invoke-AzJson -Args @("webapp", "vnet-integration", "list", "--resource-group", $ResourceGroup, "--name", $WebAppName)
    $alreadyIntegrated = $false
    if ($integrations) {
        foreach ($i in $integrations) {
            if ($i.subnetResourceId -eq $SubnetResourceId) { $alreadyIntegrated = $true; break }
        }
    }

    if ($alreadyIntegrated) {
        Log-Info "Web app is already integrated with subnet '$IntegrationSubnetName'."
        return
    }

    Log-Info "Adding Web App VNet integration..."
    Invoke-Az -Args @(
        "webapp", "vnet-integration", "add",
        "--resource-group", $ResourceGroup,
        "--name", $WebAppName,
        "--vnet", $VNetName,
        "--subnet", $IntegrationSubnetName
    )
}

function Ensure-PrivateDnsZone {
    $zone = Invoke-AzJson -Args @("network", "private-dns", "zone", "show", "--resource-group", $ResourceGroup, "--name", $PrivateDnsZoneName)
    if ($null -eq $zone) {
        if (-not $CreateMissingResources) {
            throw "Private DNS zone '$PrivateDnsZoneName' not found. Re-run with -CreateMissingResources or create manually."
        }
        Log-Info "Creating private DNS zone '$PrivateDnsZoneName'..."
        Invoke-Az -Args @("network", "private-dns", "zone", "create", "--resource-group", $ResourceGroup, "--name", $PrivateDnsZoneName)
    }
    else {
        Log-Info "Private DNS zone '$PrivateDnsZoneName' already exists."
    }
}

function Ensure-PrivateDnsZoneLink {
    $link = Invoke-AzJson -Args @(
        "network", "private-dns", "link", "vnet", "show",
        "--resource-group", $ResourceGroup,
        "--zone-name", $PrivateDnsZoneName,
        "--name", $PrivateDnsZoneLinkName
    )

    if ($null -eq $link) {
        Log-Info "Creating private DNS zone link '$PrivateDnsZoneLinkName'..."
        Invoke-Az -Args @(
            "network", "private-dns", "link", "vnet", "create",
            "--resource-group", $ResourceGroup,
            "--zone-name", $PrivateDnsZoneName,
            "--name", $PrivateDnsZoneLinkName,
            "--virtual-network", $VNetName,
            "--registration-enabled", "false"
        )
    }
    else {
        Log-Info "Private DNS zone link '$PrivateDnsZoneLinkName' already exists."
    }
}

function Ensure-PrivateEndpoint {
    param([string]$SqlServerResourceId)

    $pe = Invoke-AzJson -Args @("network", "private-endpoint", "show", "--resource-group", $ResourceGroup, "--name", $PrivateEndpointName)
    if ($null -eq $pe) {
        if (-not $CreateMissingResources) {
            throw "Private endpoint '$PrivateEndpointName' not found. Re-run with -CreateMissingResources or create manually."
        }
        Log-Info "Creating private endpoint '$PrivateEndpointName'..."
        Invoke-Az -Args @(
            "network", "private-endpoint", "create",
            "--resource-group", $ResourceGroup,
            "--name", $PrivateEndpointName,
            "--location", $Location,
            "--vnet-name", $VNetName,
            "--subnet", $PrivateEndpointSubnetName,
            "--private-connection-resource-id", $SqlServerResourceId,
            "--group-id", "sqlServer",
            "--connection-name", "$SqlServerName-pe-conn"
        )
    }
    else {
        Log-Info "Private endpoint '$PrivateEndpointName' already exists."
    }
}

function Ensure-PrivateDnsZoneGroup {
    $zoneGroup = Invoke-AzJson -Args @(
        "network", "private-endpoint", "dns-zone-group", "show",
        "--resource-group", $ResourceGroup,
        "--endpoint-name", $PrivateEndpointName,
        "--name", $PrivateDnsZoneGroupName
    )

    if ($null -eq $zoneGroup) {
        Log-Info "Creating private endpoint DNS zone group..."
        Invoke-Az -Args @(
            "network", "private-endpoint", "dns-zone-group", "create",
            "--resource-group", $ResourceGroup,
            "--endpoint-name", $PrivateEndpointName,
            "--name", $PrivateDnsZoneGroupName,
            "--private-dns-zone", $PrivateDnsZoneName,
            "--zone-name", "sql"
        )
    }
    else {
        Log-Info "Private endpoint DNS zone group already exists."
    }
}

function Set-SqlPublicNetworkAccess {
    if (-not $DisableSqlPublicNetworkAccess) {
        return
    }

    Log-Warn "Disabling SQL Server public network access..."
    Invoke-Az -Args @(
        "sql", "server", "update",
        "--resource-group", $ResourceGroup,
        "--name", $SqlServerName,
        "--public-network-access", "Disabled"
    )
}

function Set-WebAppConnectionString {
    if ([string]::IsNullOrWhiteSpace($SqlConnectionString)) {
        return
    }

    Log-Info "Applying ConnectionStrings__DefaultConnection to Web App settings..."
    Invoke-Az -Args @(
        "webapp", "config", "appsettings", "set",
        "--resource-group", $ResourceGroup,
        "--name", $WebAppName,
        "--settings", "ConnectionStrings__DefaultConnection=$SqlConnectionString"
    )
}

Ensure-AzCli
Ensure-LoggedIn
Ensure-ResourceGroup

$webApp = Get-WebApp
if ($null -eq $webApp) { throw "Web App '$WebAppName' not found in resource group '$ResourceGroup'." }
Log-Info "Found Web App '$WebAppName'."

$sqlServer = Get-SqlServer
if ($null -eq $sqlServer) { throw "SQL Server '$SqlServerName' not found in resource group '$ResourceGroup'." }
Log-Info "Found SQL Server '$SqlServerName'."

$vnet = Ensure-VNet

$integrationSubnet = Ensure-Subnet `
    -SubnetName $IntegrationSubnetName `
    -AddressPrefix $IntegrationSubnetPrefix `
    -Delegation "Microsoft.Web/serverFarms" `
    -DisablePrivateEndpointNetworkPolicies:$false

$privateEndpointSubnet = Ensure-Subnet `
    -SubnetName $PrivateEndpointSubnetName `
    -AddressPrefix $PrivateEndpointSubnetPrefix `
    -Delegation "" `
    -DisablePrivateEndpointNetworkPolicies:$true

Ensure-WebAppVNetIntegration -SubnetResourceId $integrationSubnet.id
Ensure-PrivateDnsZone
Ensure-PrivateDnsZoneLink
Ensure-PrivateEndpoint -SqlServerResourceId $sqlServer.id
Ensure-PrivateDnsZoneGroup
Set-SqlPublicNetworkAccess
Set-WebAppConnectionString

Log-Info "Completed."
if ($WhatIf) {
    Log-Warn "This was a dry-run. Re-run without -WhatIf to apply changes."
}
