#!/usr/bin/env bash
set -euo pipefail
for rid in linux-x64 linux-arm64; do out="artifacts/agent/$rid"; dotnet publish src/FrogPlug.Agent -c Release -r "$rid" --self-contained -p:PublishSingleFile=true -o "$out"; sha256sum "$out/frogplug-agent"; done
