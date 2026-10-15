param([string[]]$Rid = @("linux-x64", "linux-arm64"))

& "$PSScriptRoot/fetch-xray.ps1"
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

foreach ($currentRid in $Rid)
{
    $agentDir = "artifacts/agent/$currentRid"

    dotnet publish src/FrogPlug.Agent -c Release -r $currentRid --self-contained `
        -p:PublishSingleFile=true -o $agentDir
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }
}

foreach ($currentRid in $Rid)
{
    $panelDir = "artifacts/panel/$currentRid"

    dotnet publish src/FrogPlug.Server -c Release -r $currentRid --self-contained `
        -p:PublishSingleFile=true -o $panelDir
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }

    foreach ($agentRid in $Rid)
    {
        New-Item -ItemType Directory -Force -Path "$panelDir/agent/$agentRid" | Out-Null
        Copy-Item "artifacts/agent/$agentRid/FrogPlug.Agent" `
            "$panelDir/agent/$agentRid/frogplug-agent" -Force
    }

    New-Item -ItemType Directory -Force -Path "$panelDir/deploy" | Out-Null
    Copy-Item deploy/agent-install.sh, deploy/backup.sh, deploy/xray-core.json "$panelDir/deploy/" -Force
    Copy-Item deploy/frogplug-backup.service, deploy/frogplug-backup.timer "$panelDir/deploy/" -Force
    Copy-Item artifacts/xray-core "$panelDir/xray-core" -Recurse -Force
    $settings = "{`n  \"Agent\": {`n    \"ArtifactsPath\": \"agent\"`n  },`n  \"XrayCore\": {`n    \"BundledPath\": \"xray-core\"`n  }`n}`n"
    [IO.File]::WriteAllText(
        "$panelDir/appsettings.Production.json",
        $settings,
        [Text.UTF8Encoding]::new($false))
    tar -C artifacts/panel -czf "artifacts/frogplug-$currentRid.tar.gz" $currentRid
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }

    Get-FileHash "artifacts/frogplug-$currentRid.tar.gz" -Algorithm SHA256
}
