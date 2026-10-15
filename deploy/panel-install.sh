#!/usr/bin/env bash
set -euo pipefail

[[ $EUID -eq 0 ]] || { echo "Запустите от root" >&2; exit 1; }
domain=""
archive=""
email=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --domain) domain="${2:-}"; shift 2 ;;
        --archive) archive="${2:-}"; shift 2 ;;
        --email) email="${2:-}"; shift 2 ;;
        *) echo "Неизвестный аргумент: $1" >&2; exit 2 ;;
    esac
done
[[ -n "$domain" && -n "$archive" ]] || {
    echo "Нужны --domain и --archive" >&2
    exit 2
}
[[ "$domain" =~ ^[A-Za-z0-9.-]+$ ]] || {
    echo "Некорректный домен" >&2
    exit 2
}
source /etc/os-release
if [[ "$ID" == "debian" && "${VERSION_ID%%.*}" -ge 12 ]]; then
    :
elif [[ "$ID" == "ubuntu" && "${VERSION_ID%%.*}" -ge 22 ]]; then
    :
else
    echo "Поддерживаются Debian 12+ и Ubuntu 22.04+" >&2
    exit 1
fi

apt-get update
apt-get install -y ca-certificates curl gpg sqlite3 tar
id -u frogplug >/dev/null 2>&1 || useradd --system --home-dir /var/lib/frogplug \
    --create-home --shell /usr/sbin/nologin frogplug
install -d -o frogplug -g frogplug -m 0750 /var/lib/frogplug /var/lib/frogplug/keys
tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT
if [[ "$archive" =~ ^https?:// ]]; then
    curl -fsSL "$archive" -o "$tmpdir/panel.tar.gz"
else
    cp -- "$archive" "$tmpdir/panel.tar.gz"
fi
tar -xzf "$tmpdir/panel.tar.gz" -C "$tmpdir"
source_dir="$(find "$tmpdir" -mindepth 1 -maxdepth 1 -type d -name 'linux-*' | head -1)"
[[ -n "$source_dir" && -x "$source_dir/FrogPlug.Server" ]] || {
    echo "В архиве не найден каталог linux-*/FrogPlug.Server" >&2
    exit 1
}
systemctl stop frogplug.service 2>/dev/null || true
install -d /opt/frogplug
find /opt/frogplug -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +
cp -a "$source_dir"/. /opt/frogplug/
chown -R root:root /opt/frogplug
chown -R frogplug:frogplug /var/lib/frogplug
install -m 0755 /opt/frogplug/deploy/backup.sh /usr/local/sbin/frogplug-backup
install -m 0644 /opt/frogplug/deploy/frogplug-backup.service \
    /etc/systemd/system/frogplug-backup.service
install -m 0644 /opt/frogplug/deploy/frogplug-backup.timer \
    /etc/systemd/system/frogplug-backup.timer

cat > /etc/systemd/system/frogplug.service <<'EOF'
[Unit]
Description=FrogPlug
After=network-online.target
Wants=network-online.target
[Service]
User=frogplug
WorkingDirectory=/opt/frogplug
ExecStart=/opt/frogplug/FrogPlug.Server
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5280
Environment=DataDirectory=/var/lib/frogplug
Restart=always
NoNewPrivileges=true
ProtectSystem=strict
ReadWritePaths=/var/lib/frogplug
PrivateTmp=true
[Install]
WantedBy=multi-user.target
EOF

if ! command -v caddy >/dev/null 2>&1; then
    apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' |
        gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
        > /etc/apt/sources.list.d/caddy-stable.list
    apt-get update
    apt-get install -y caddy
fi
if [[ -n "$email" ]]; then
    cat > /etc/caddy/Caddyfile <<EOF
{
    email $email
}

$domain {
    reverse_proxy 127.0.0.1:5280
}
EOF
else
    cat > /etc/caddy/Caddyfile <<EOF
$domain {
    reverse_proxy 127.0.0.1:5280
}
EOF
fi
systemctl daemon-reload
systemctl enable --now frogplug.service
systemctl enable caddy
systemctl restart caddy
systemctl enable --now frogplug-backup.timer
if command -v ufw >/dev/null 2>&1 && ufw status | grep -q active; then
    ufw allow 80/tcp
    ufw allow 443/tcp
fi
echo "Панель установлена: https://$domain"
echo "$(PublicBaseUrl="https://$domain" /opt/frogplug/FrogPlug.Server setup-link)"
