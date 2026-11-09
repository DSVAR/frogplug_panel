#!/usr/bin/env bash
set -euo pipefail

readonly ROOT_DIR="/opt/frogplug-docker"
readonly ENV_FILE="${ROOT_DIR}/.env"
readonly NODE_CONFIG_DIR="/etc/frogplug-node"
readonly NODE_DATA_DIR="/var/lib/frogplug-node"
with_panel=false
domain=""
panel_url=""
token=""
images=""
registry=""
tag=""
SUBSCRIPTION_PATH=""

usage() {
    cat <<'EOF'
Использование:
  install.sh --with-panel --domain example.com --images images.tar
  install.sh --panel-url https://panel.example.com/secret --token TOKEN --registry registry.example.com/vpn --tag VERSION
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --with-panel) with_panel=true; shift ;;
        --domain) domain="${2:-}"; shift 2 ;;
        --panel-url) panel_url="${2:-}"; shift 2 ;;
        --token) token="${2:-}"; shift 2 ;;
        --images) images="${2:-}"; shift 2 ;;
        --registry) registry="${2:-}"; shift 2 ;;
        --tag) tag="${2:-}"; shift 2 ;;
        --help) usage; exit 0 ;;
        *) usage >&2; exit 2 ;;
    esac
done

[[ ${EUID} -eq 0 ]] || { echo "Запустите скрипт от root." >&2; exit 1; }
[[ -n "$images" || -n "$registry" ]] || { echo "Укажите --images или --registry." >&2; exit 2; }
[[ -z "$images" || -z "$registry" ]] || { echo "Укажите только один источник образов." >&2; exit 2; }
 # Первый запуск и обновление.
if [[ "$with_panel" == true ]]; then
    # Секретные пути.
    [[ "$domain" =~ ^[A-Za-z0-9.-]+$ ]] || { echo "Для --with-panel нужен корректный --domain." >&2; exit 2; }
else
    [[ "$panel_url" =~ ^https?:// ]] && [[ -n "$token" ]] || { echo "Для узла нужны --panel-url и --token." >&2; exit 2; }
fi

source /etc/os-release
case "$ID" in debian|ubuntu) ;; *) echo "Поддерживаются только Debian и Ubuntu." >&2; exit 1 ;; esac

# Установка Docker.
install_docker() {
    if command -v docker >/dev/null 2>&1; then return; fi
    apt-get -o DPkg::Lock::Timeout=300 update
    apt-get -o DPkg::Lock::Timeout=300 install -y ca-certificates curl gnupg
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL "https://download.docker.com/linux/${ID}/gpg" | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    chmod a+r /etc/apt/keyrings/docker.gpg
    local codename="${VERSION_CODENAME:-$UBUNTU_CODENAME}"
    printf '%s\n' "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/${ID} ${codename} stable" > /etc/apt/sources.list.d/docker.list
    apt-get -o DPkg::Lock::Timeout=300 update
    apt-get -o DPkg::Lock::Timeout=300 install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
}

# Фаервол.
configure_firewall() {
    local ssh_port
    ssh_port="$(sshd -T 2>/dev/null | awk '/^port / && !found { print $2; found = 1 }')"
    ssh_port="${ssh_port:-22}"
    # При обновлении пакеты уже стоят: apt не трогаем, чтобы не ждать чужую блокировку dpkg.
    if ! command -v ufw >/dev/null 2>&1 || ! command -v iptables >/dev/null 2>&1; then
        apt-get -o DPkg::Lock::Timeout=300 update
        apt-get -o DPkg::Lock::Timeout=300 install -y ufw iptables
    fi
    ufw default deny incoming
    ufw allow "${ssh_port}/tcp"
    ufw allow 80/tcp
    ufw allow 443/tcp
    ufw allow "${NODE_EXTRA_PORTS:-443/udp}"
    ufw --force enable
}

prepare_repository() {
    local source_dir saved_env
    source_dir="$(cd "$(dirname "$0")/.." && pwd)"
    saved_env="$(mktemp)"
    [[ -f "$ENV_FILE" ]] && cp "$ENV_FILE" "$saved_env"
    rm -rf "$ROOT_DIR"
    install -d -m 0755 "$ROOT_DIR/deploy/docker"
    cp "$source_dir/deploy/docker/compose.panel.yml" "$ROOT_DIR/deploy/docker/"
    cp "$source_dir/deploy/docker/compose.node.yml" "$ROOT_DIR/deploy/docker/"
    [[ -s "$saved_env" ]] && cp "$saved_env" "$ENV_FILE"
    rm -f "$saved_env"
}

prepare_node_directories() {
    install -d -m 0750 "${NODE_CONFIG_DIR}/agent" "${NODE_CONFIG_DIR}/caddy" "${NODE_CONFIG_DIR}/haproxy" "${NODE_CONFIG_DIR}/xray" "${NODE_CONFIG_DIR}/hysteria" "${NODE_CONFIG_DIR}/openflux" "${NODE_CONFIG_DIR}/supervisor" "$NODE_DATA_DIR" "${NODE_DATA_DIR}/openflux"
    chown -R 10001:10001 "$NODE_CONFIG_DIR" "$NODE_DATA_DIR"
    touch "${NODE_CONFIG_DIR}/mtg.toml"
    chown 10001:10001 "${NODE_CONFIG_DIR}/mtg.toml"
    chmod 0600 "${NODE_CONFIG_DIR}/mtg.toml"
    [[ -s "${NODE_CONFIG_DIR}/haproxy/haproxy.cfg" ]] || cat > "${NODE_CONFIG_DIR}/haproxy/haproxy.cfg" <<'EOF'
global
    daemon

defaults
    mode tcp
    timeout connect 5s
    timeout client 1h
    timeout server 1h
    timeout tunnel 1h
    timeout client-fin 30s

frontend frogplug-https
    bind :443
    tcp-request inspect-delay 5s
    tcp-request content accept if { req_ssl_hello_type 1 }
    default_backend frogplug-reality

backend frogplug-reality
    mode tcp
    server reality 127.0.0.1:4431 send-proxy-v2
EOF
    # Начальные файлы только для чистой установки: при обновлении рабочие конфиги агента не трогаем.
    [[ -s "${NODE_CONFIG_DIR}/caddy/Caddyfile" ]] || printf ':80 {\n    respond 404\n}\n' > "${NODE_CONFIG_DIR}/caddy/Caddyfile"
    [[ -s "${NODE_CONFIG_DIR}/xray/config.json" ]] ||
        printf '%s\n' '{"inbounds":[],"outbounds":[{"protocol":"freedom"}]}' > "${NODE_CONFIG_DIR}/xray/config.json"
}

configure_panel_proxy() {
    # Пока агент не применил настоящий конфиг, весь 443 идёт прямо в Caddy, чтобы ссылка первой настройки открылась.
    sed -i "s#default_backend frogplug-reality#default_backend frogplug-caddy#" "${NODE_CONFIG_DIR}/haproxy/haproxy.cfg"
    printf '\nbackend frogplug-caddy\n    mode tcp\n    server caddy 127.0.0.1:8443 send-proxy-v2\n' >> "${NODE_CONFIG_DIR}/haproxy/haproxy.cfg"
    cat > "${NODE_CONFIG_DIR}/caddy/Caddyfile" <<EOF
{
    servers {
        listener_wrappers {
            proxy_protocol {
                allow 127.0.0.1/32
            }
            tls
        }
    }
}

http://${domain} {
    redir https://{host}{uri} permanent
}

https://${domain}:8443 {
    bind 127.0.0.1
    handle ${PANEL_PATH}* {
        reverse_proxy 127.0.0.1:5280
    }
    handle ${SUBSCRIPTION_PATH}* {
        reverse_proxy 127.0.0.1:5280
    }
    handle {
        root * /var/lib/frogplug/site
        try_files {path} {path}/index.html
        file_server
    }
    handle_errors {
        root * /var/lib/frogplug/site
        rewrite * /404.html
        file_server
    }
}
EOF
}

load_images() {
    if [[ -n "$images" ]]; then
        local loaded
        loaded="$(docker load -i "$images")"
        PANEL_IMAGE="$(awk '/Loaded image: frogplug\/panel:/ { print $3 }' <<< "$loaded" | tail -1)"
        NODE_IMAGE="$(awk '/Loaded image: frogplug\/node:/ { print $3 }' <<< "$loaded" | tail -1)"
    else
        [[ -n "$tag" ]] || { echo "Для --registry укажите --tag." >&2; exit 2; }
        PANEL_IMAGE="${registry}/panel:${tag}"
        NODE_IMAGE="${registry}/node:${tag}"
        docker pull "$PANEL_IMAGE"
        docker pull "$NODE_IMAGE"
    fi
    [[ -n "$PANEL_IMAGE" && -n "$NODE_IMAGE" ]] || { echo "В архиве нет нужных образов." >&2; exit 1; }
}

write_env() {
    local public_base_url="$1" node_panel_url="$2" node_token="$3" panel_upstream="$4"
    cat > "$ENV_FILE" <<EOF
PUBLIC_BASE_URL=${public_base_url}
PANEL_URL=${node_panel_url}
AGENT_TOKEN=${node_token}
PANEL_PATH=${PANEL_PATH}
SUBSCRIPTION_PATH=${SUBSCRIPTION_PATH}
PANEL_IMAGE=${PANEL_IMAGE}
NODE_IMAGE=${NODE_IMAGE}
PanelUpstream=${panel_upstream}
NODE_CONFIG_DIR=${NODE_CONFIG_DIR}
NODE_DATA_DIR=${NODE_DATA_DIR}
EOF
    chmod 0600 "$ENV_FILE"
}

wait_for_panel() {
    local attempt
    for attempt in $(seq 1 30); do
        if docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/deploy/docker/compose.panel.yml" ps --status running panel | grep panel >/dev/null; then return; fi
        sleep 2
    done
    echo "Панель не стала доступна." >&2
    exit 1
}

install_docker
configure_firewall
prepare_repository
prepare_node_directories
load_images

if [[ "$with_panel" == true ]]; then
    # Секретный путь создаётся один раз и при повторных запусках берётся из готового env-файла.
    # od читает байты без конвейера: pipefail не прерывает установку.
    PANEL_PATH="$(grep -m1 '^PANEL_PATH=' "$ENV_FILE" 2>/dev/null | cut -d= -f2- || true)"
    if [[ ! "$PANEL_PATH" =~ ^/[a-z0-9]{16,}$ ]]; then
        PANEL_PATH="/$(od -An -N8 -tx1 /dev/urandom | tr -d ' \n')"
    fi
    SUBSCRIPTION_PATH="$(grep -m1 '^SUBSCRIPTION_PATH=' "$ENV_FILE" 2>/dev/null | cut -d= -f2- || true)"
    if [[ ! "$SUBSCRIPTION_PATH" =~ ^/[a-z0-9]{16,}$ || "$SUBSCRIPTION_PATH" == "$PANEL_PATH" ]]; then
        SUBSCRIPTION_PATH="/$(od -An -N8 -tx1 /dev/urandom | tr -d ' \n')"
    fi
    # Повторный запуск = обновление: токен локальной ноды уже есть, её не создаём заново,
    # а рабочие конфиги HAProxy/Caddy не затираем начальными.
    token="$(grep -m1 '^AGENT_TOKEN=' "$ENV_FILE" 2>/dev/null | cut -d= -f2- || true)"
    write_env "https://${domain}${PANEL_PATH}" "http://127.0.0.1:5280${PANEL_PATH}" "$token" "127.0.0.1:5280"
    if [[ -z "$token" ]]; then
        configure_panel_proxy
    fi
    docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/deploy/docker/compose.panel.yml" up -d
    wait_for_panel
    if [[ -z "$token" ]]; then
        public_ip="$(curl -4fsSL https://api.ipify.org)"
        token="$(docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/deploy/docker/compose.panel.yml" exec -T panel /usr/local/bin/FrogPlug.Server add-local-node --name local --domain "$domain" --address "$public_ip")"
        write_env "https://${domain}${PANEL_PATH}" "http://127.0.0.1:5280${PANEL_PATH}" "$token" "127.0.0.1:5280"
        docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/deploy/docker/compose.node.yml" up -d
        echo "Первая настройка: $(docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/deploy/docker/compose.panel.yml" exec -T panel /usr/local/bin/FrogPlug.Server setup-link)"
    else
        docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/deploy/docker/compose.node.yml" up -d
        echo "Обновление завершено. Панель: https://${domain}${PANEL_PATH}/"
    fi
else
    panel_url_path="${panel_url#*://}"
    if [[ "$panel_url_path" == */* ]]; then
        PANEL_PATH="/${panel_url_path#*/}"
    else
        PANEL_PATH=""
    fi
    write_env "" "$panel_url" "$token" ""
    docker compose --env-file "$ENV_FILE" -f "$ROOT_DIR/deploy/docker/compose.node.yml" up -d
fi
