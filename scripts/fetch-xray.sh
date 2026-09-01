#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
config="$root/deploy/xray-core.json"
version="${1:-$(sed -nE 's/.*"version": "([^"]+)".*/\1/p' "$config")}"
[[ "$version" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]] || {
    echo "Некорректная версия Xray: $version" >&2
    exit 2
}

fetch() {
    local rid="$1"
    local asset="$2"
    local directory="$root/artifacts/xray-core/$version/$rid"
    local url="https://github.com/XTLS/Xray-core/releases/download/$version/$asset"
    local archive="$directory/$asset"
    local digest="$directory/$asset.dgst"

    install -d "$directory"
    curl -fsSL "$url" -o "$archive"
    curl -fsSL "$url.dgst" -o "$digest"
    local expected
    expected="$(grep -oE 'SHA2-256= *[0-9a-fA-F]{64}' "$digest" | head -1 | cut -d= -f2 | tr -d ' ' | tr '[:upper:]' '[:lower:]')"
    local actual
    actual="$(sha256sum "$archive" | cut -d' ' -f1)"
    [[ "$expected" == "$actual" ]] || {
        rm -f "$archive" "$digest"
        echo "Контрольная сумма Xray не совпала: $rid" >&2
        exit 1
    }
    printf '%s\n' "$actual" > "$archive.sha256"
    rm -f "$digest"
}

asset_x64="$(sed -nE 's/.*"linux-x64": "([^"]+)".*/\1/p' "$config")"
asset_arm64="$(sed -nE 's/.*"linux-arm64": "([^"]+)".*/\1/p' "$config")"
[[ -n "$asset_x64" && -n "$asset_arm64" ]] || {
    echo "Не найдены архивы Xray в $config" >&2
    exit 2
}
fetch linux-x64 "$asset_x64"
fetch linux-arm64 "$asset_arm64"
