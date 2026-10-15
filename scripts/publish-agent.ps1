param([string]$Rid = "linux-x64")
$out = "artifacts/agent/$Rid"; dotnet publish src/FrogPlug.Agent -c Release -r $Rid --self-contained -p:PublishSingleFile=true -o $out
Get-FileHash "$out/frogplug-agent" -Algorithm SHA256
