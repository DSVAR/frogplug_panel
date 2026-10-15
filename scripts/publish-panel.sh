#!/usr/bin/env bash
set -euo pipefail

bash scripts/fetch-xray.sh

rids=("$@")
if [[ ${#rids[@]} -eq 0 ]]; then
    rids=(linux-x64 linux-arm64)
fi

for rid in "${rids[@]}"; do
    [[ "$rid" == "linux-x64" || "$rid" == "linux-arm64" ]] || {
        echo "Неподдерживаемый RID: $rid" >&2
        exit 2
    }
    dotnet publish src/FrogPlug.Agent -c Release -r "$rid" --self-contained \
        -p:PublishSingleFile=true -o "artifacts/agent/$rid"
done

for rid in "${rids[@]}"; do
    panel_dir="artifacts/panel/$rid"
    dotnet publish src/FrogPlug.Server -c Release -r "$rid" --self-contained \
        -p:PublishSingleFile=true -o "$panel_dir"

    for agent_rid in "${rids[@]}"; do
        install -d "$panel_dir/agent/$agent_rid"
        install -m 0755 "artifacts/agent/$agent_rid/FrogPlug.Agent" \
            "$panel_dir/agent/$agent_rid/frogplug-agent"
    done
    install -d "$panel_dir/deploy"
    install -m 0755 deploy/agent-install.sh deploy/backup.sh "$panel_dir/deploy/"
    install -m 0644 deploy/xray-core.json "$panel_dir/deploy/"
    install -m 0644 deploy/frogplug-backup.service deploy/frogplug-backup.timer "$panel_dir/deploy/"
    cp -a artifacts/xray-core "$panel_dir/xray-core"
    cat > "$panel_dir/appsettings.Production.json" <<'EOF'
{
  "Agent": {
    "ArtifactsPath": "agent"
  },
  "XrayCore": {
    "BundledPath": "xray-core"
  }
}
EOF
    tar -C artifacts/panel -czf "artifacts/frogplug-$rid.tar.gz" "$rid"
    sha256sum "artifacts/frogplug-$rid.tar.gz"
done
