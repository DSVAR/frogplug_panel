#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
arch="amd64"
if [[ "${1:-}" == "--arch" ]]; then arch="${2:-}"; fi
[[ "$arch" == amd64 || "$arch" == arm64 ]] || { echo "Поддерживаются amd64 и arm64." >&2; exit 2; }
version="$(git describe --tags --always 2>/dev/null || date -u +%Y%m%d%H%M%S)"
bash "$root/scripts/publish-panel.sh"
bash "$root/scripts/fetch-xray.sh"
source "$root/deploy/docker/versions.env"
XRAY_VERSION="${XRAY_VERSION:-v25.10.15}"
docker buildx build --platform "linux/$arch" --load -f "$root/deploy/docker/panel/Dockerfile" -t "frogplug/panel:$version" "$root"
node_args=()
while IFS='=' read -r name _; do
    [[ -n "$name" && "$name" != \#* ]] || continue
    node_args+=(--build-arg "$name=${!name}")
done < "$root/deploy/docker/versions.env"
docker buildx build --platform "linux/$arch" --load -f "$root/deploy/docker/node/Dockerfile" "${node_args[@]}" -t "frogplug/node:$version" "$root"
mkdir -p "$root/artifacts"
docker save "frogplug/panel:$version" "frogplug/node:$version" -o "$root/artifacts/images-$version.tar"
tar -czf "$root/artifacts/installer-$version.tar.gz" -C "$root" deploy/install.sh deploy/docker/compose.panel.yml deploy/docker/compose.node.yml deploy/docker/versions.env
echo "$version"
