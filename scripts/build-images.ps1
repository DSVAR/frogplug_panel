[CmdletBinding()]
param(
    [ValidateSet("amd64")]
    [string]$Arch = "amd64",
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = (& git describe --tags --always 2>$null | Select-Object -Last 1).Trim()
        if ([string]::IsNullOrWhiteSpace($Version)) {
            $Version = Get-Date -Format "yyyyMMddHHmmss"
        }
    }

    & "$PSScriptRoot/fetch-xray.ps1"
    & "$PSScriptRoot/publish-agent.ps1" -Rid "linux-x64"

    docker build --platform "linux/$Arch" `
        -f deploy/docker/panel/Dockerfile `
        -t "frogplug/panel:$Version" .

    $nodeBuildArgs = @()
    Get-Content deploy/docker/versions.env |
        Where-Object { $_ -match '^[A-Z0-9_]+=' } |
        ForEach-Object {
            $nodeBuildArgs += "--build-arg"
            $nodeBuildArgs += $_
        }
    $xrayVersion = (Get-Content deploy/xray-core.json -Raw | ConvertFrom-Json).version
    $nodeBuildArgs += "--build-arg"
    $nodeBuildArgs += "XRAY_VERSION=$xrayVersion"

    docker build --platform "linux/$Arch" `
        -f deploy/docker/node/Dockerfile `
        @nodeBuildArgs `
        -t "frogplug/node:$Version" .

    New-Item -ItemType Directory -Force artifacts | Out-Null
    $images = "artifacts/images-$Version.tar"
    $installer = "artifacts/installer-$Version.tar.gz"
    docker save "frogplug/panel:$Version" "frogplug/node:$Version" -o $images
    tar -czf $installer deploy/install.sh deploy/docker/compose.panel.yml `
        deploy/docker/compose.node.yml deploy/docker/versions.env

    Write-Host ""
    Write-Host "Done:"
    Write-Host (Join-Path $root $images)
    Write-Host (Join-Path $root $installer)
}
finally {
    Pop-Location
}
