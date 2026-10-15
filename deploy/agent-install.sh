#!/usr/bin/env bash
set -euo pipefail

PANEL_URL="${FROGPLUG_PANEL_URL:-}"
TOKEN=""
xray_source="panel"
XRAY_VERSION=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --panel-url) PANEL_URL="$2"; shift 2 ;;
        --token) TOKEN="$2"; shift 2 ;;
        --xray-version) XRAY_VERSION="$2"; shift 2 ;;
        --xray-source) xray_source="$2"; shift 2 ;;
        *) echo "Неизвестный аргумент: $1" >&2; exit 2 ;;
    esac
done
[[ $EUID -eq 0 ]] || { echo "Запустите от root" >&2; exit 1; }
[[ -n "$PANEL_URL" && -n "$TOKEN" ]] || { echo "Нужны --panel-url и --token" >&2; exit 2; }
[[ "$xray_source" == "panel" || "$xray_source" == "github" ]] || {
    echo "Источник Xray должен быть panel или github" >&2
    exit 2
}

apt-get update
apt-get install -y curl unzip ca-certificates apt-transport-https haproxy ufw
arch="$(uname -m)"
case "$arch" in
    x86_64) rid="linux-x64"; xray_asset="Xray-linux-64.zip" ;;
    aarch64) rid="linux-arm64"; xray_asset="Xray-linux-arm64-v8a.zip" ;;
    *) echo "Архитектура $arch не поддерживается" >&2; exit 1 ;;
esac
install -d -m 0755 /etc/frogplug-agent /usr/local/etc/xray /usr/local/share/xray
id -u xray >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin xray

tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT
curl -fsSL "${PANEL_URL%/}/agent/download/${rid}" -o "$tmpdir/agent"
install -m 0755 "$tmpdir/agent" /usr/local/bin/frogplug-agent

if [[ -z "$XRAY_VERSION" ]]; then
    XRAY_VERSION="$(curl -fsSL "${PANEL_URL%/}/agent/xray/default")"
fi
[[ "$XRAY_VERSION" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]] || {
    echo "Некорректная версия Xray: $XRAY_VERSION" >&2
    exit 1
}
if [[ "$xray_source" == "github" ]]; then
    xray_url="https://github.com/XTLS/Xray-core/releases/download/${XRAY_VERSION}/${xray_asset}"
    checksum_url="${xray_url}.dgst"
else
    xray_url="${PANEL_URL%/}/agent/xray/${XRAY_VERSION}/${rid}"
    checksum_url="${xray_url}.sha256"
fi
curl -fsSL "$xray_url" -o "$tmpdir/xray.zip"
curl -fsSL "$checksum_url" -o "$tmpdir/xray.dgst"
if [[ "$xray_source" == "github" ]]; then
    expected="$(grep -oE 'SHA2-256= *[0-9a-fA-F]{64}' "$tmpdir/xray.dgst" | head -1 | cut -d= -f2 | tr -d ' ')"
else
    expected="$(tr -d '[:space:]' < "$tmpdir/xray.dgst")"
fi
actual="$(sha256sum "$tmpdir/xray.zip" | cut -d' ' -f1)"
[[ "${expected,,}" == "$actual" ]] || { echo "Неверная контрольная сумма Xray" >&2; exit 1; }
unzip -oq "$tmpdir/xray.zip" xray -d "$tmpdir/xray"
install -m 0755 "$tmpdir/xray/xray" /usr/local/bin/xray

if ! command -v caddy >/dev/null 2>&1; then
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' > /etc/apt/sources.list.d/caddy-stable.list
    apt-get update
    apt-get install -y caddy
fi

cat > /etc/frogplug-agent/agent.json <<EOF
{
  "Listen": "127.0.0.1:9080",
  "Token": "$TOKEN",
  "PanelUrl": "$PANEL_URL",
  "XrayBinary": "/usr/local/bin/xray",
  "XrayConfigPath": "/usr/local/etc/xray/config.json",
  "GeoDir": "/usr/local/share/xray",
  "CaddyfilePath": "/etc/caddy/Caddyfile"
}
EOF
chmod 600 /etc/frogplug-agent/agent.json

cat > /etc/systemd/system/frogplug-agent.service <<'EOF'
[Unit]
Description=FrogPlug agent
Wants=network-online.target
After=network-online.target
[Service]
ExecStart=/usr/local/bin/frogplug-agent
Restart=always
[Install]
WantedBy=multi-user.target
EOF
if [[ ! -f /etc/systemd/system/xray.service ]]; then
    cat > /etc/systemd/system/xray.service <<'EOF'
[Unit]
After=network.target
[Service]
User=xray
ExecStart=/usr/local/bin/xray run -config /usr/local/etc/xray/config.json
Restart=on-failure
LimitNOFILE=1048576
[Install]
WantedBy=multi-user.target
EOF
fi
printf 'net.core.default_qdisc = fq
net.ipv4.tcp_congestion_control = bbr
' > /etc/sysctl.d/90-frogplug.conf
sysctl --system > /dev/null || true
systemctl daemon-reload
systemctl enable frogplug-agent.service
systemctl restart frogplug-agent.service
systemctl enable xray.service
ssh_port="$(sshd -T 2>/dev/null | awk '/^port /{print $2; exit}' || true)"
ssh_port="${ssh_port:-22}"
ufw default deny incoming
ufw allow "${ssh_port}/tcp"
ufw allow 80/tcp
ufw allow 443/tcp
ufw delete allow 8443/tcp || true
ufw delete allow 2443/tcp || true
ufw --force enable
