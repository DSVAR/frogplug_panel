FROM debian:bookworm-slim AS downloads

ARG TARGETARCH
ARG CADDY_VERSION=2.8.4
ARG CADDY_SHA256_AMD64
ARG CADDY_SHA256_ARM64
ARG MTG_VERSION=v2.3.0
ARG MTG_SHA256_AMD64
ARG MTG_SHA256_ARM64
ARG OPENFLUX_VERSION=node-v1.7.3
ARG OPENFLUX_SHA256_AMD64
ARG OPENFLUX_SHA256_ARM64
ARG HYSTERIA_VERSION=v2.13.0
ARG HYSTERIA_SHA256_AMD64
ARG HYSTERIA_SHA256_ARM64

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl unzip \
    && rm -rf /var/lib/apt/lists/*
RUN set -eux; \
    case "$TARGETARCH" in \
        amd64) caddy_arch=amd64; caddy_sha="$CADDY_SHA256_AMD64"; mtg_arch=amd64; mtg_sha="$MTG_SHA256_AMD64"; openflux_arch=amd64; openflux_sha="$OPENFLUX_SHA256_AMD64"; hysteria_arch=amd64; hysteria_sha="$HYSTERIA_SHA256_AMD64" ;; \
        arm64) caddy_arch=arm64; caddy_sha="$CADDY_SHA256_ARM64"; mtg_arch=arm64; mtg_sha="$MTG_SHA256_ARM64"; openflux_arch=arm64; openflux_sha="$OPENFLUX_SHA256_ARM64"; hysteria_arch=arm64; hysteria_sha="$HYSTERIA_SHA256_ARM64" ;; \
        *) echo "Неподдерживаемая архитектура: $TARGETARCH" >&2; exit 1 ;; \
    esac; \
    test -n "$caddy_sha" -a -n "$mtg_sha" -a -n "$openflux_sha" -a -n "$hysteria_sha"; \
    curl -fsSL "https://github.com/caddyserver/caddy/releases/download/v${CADDY_VERSION}/caddy_${CADDY_VERSION}_linux_${caddy_arch}.tar.gz" -o /tmp/caddy.tar.gz; \
    echo "$caddy_sha  /tmp/caddy.tar.gz" | sha256sum -c -; \
    tar -xzf /tmp/caddy.tar.gz -C /tmp caddy; \
    install -Dm0755 /tmp/caddy /out/caddy; \
    curl -fsSL "https://github.com/9seconds/mtg/releases/download/${MTG_VERSION}/mtg-${MTG_VERSION#v}-linux-${mtg_arch}.tar.gz" -o /tmp/mtg.tar.gz; \
    echo "$mtg_sha  /tmp/mtg.tar.gz" | sha256sum -c -; \
    tar -xzf /tmp/mtg.tar.gz -C /tmp; \
    install -Dm0755 /tmp/mtg-${MTG_VERSION#v}-linux-${mtg_arch}/mtg /out/mtg; \
    curl -fsSL "https://github.com/meepo161/OpenFlux/releases/download/${OPENFLUX_VERSION}/openflux-linux-${openflux_arch}" -o /tmp/openflux; \
    echo "$openflux_sha  /tmp/openflux" | sha256sum -c -; \
    install -Dm0755 /tmp/openflux /out/openflux; \
    curl -fsSL "https://github.com/apernet/hysteria/releases/download/app%2F${HYSTERIA_VERSION}/hysteria-linux-${hysteria_arch}" -o /tmp/hysteria; \
    echo "$hysteria_sha  /tmp/hysteria" | sha256sum -c -; \
    install -Dm0755 /tmp/hysteria /out/hysteria

FROM golang:1.26-bookworm AS tgweb-build

ARG TPROXY_REF=52a5feb7fac38f68da5afef9cedd9b3bfc8473ca

RUN set -eux; \
    git clone https://github.com/telegramdesktop/tproxy-server.git /src/tproxy-server; \
    cd /src/tproxy-server; \
    git checkout --detach "$TPROXY_REF"; \
    test "$(git rev-parse HEAD)" = "$TPROXY_REF"; \
    CGO_ENABLED=0 go build -trimpath -ldflags='-s -w' -o /out/tproxy-server ./cmd/tproxy-server

FROM debian:bookworm AS mtproxy-build

ARG MTPROXY_REF=69f2f083b57bbd94932cb9f4f32155f33e719ff1

RUN apt-get update \
    && apt-get install -y --no-install-recommends build-essential ca-certificates git libssl-dev zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*
RUN set -eux; \
    git clone https://github.com/TelegramMessenger/MTProxy.git /src/MTProxy; \
    cd /src/MTProxy; \
    git checkout --detach "$MTPROXY_REF"; \
    test "$(git rev-parse HEAD)" = "$MTPROXY_REF"; \
    make -j"$(nproc)"; \
    install -Dm0755 objs/bin/mtproto-proxy /out/mtproto-proxy

FROM debian:bookworm-slim

ARG TARGETARCH
ARG XRAY_VERSION=v25.10.15

RUN apt-get update \
    && apt-get install -y --no-install-recommends haproxy supervisor ca-certificates curl libcap2-bin unzip libicu72 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=downloads /out/caddy /usr/local/bin/caddy
COPY --from=downloads /out/mtg /usr/local/bin/mtg
COPY --from=downloads /out/openflux /opt/openflux-node/bin/openflux
COPY --from=downloads /out/hysteria /usr/local/bin/hysteria
COPY --from=tgweb-build /out/tproxy-server /usr/local/bin/tproxy-server
COPY --from=mtproxy-build /out/mtproto-proxy /usr/local/bin/mtproto-proxy
COPY deploy/docker/node/mtproxy-bootstrap /usr/share/mtproxy-bootstrap
COPY artifacts/agent /opt/frogplug-agent
COPY artifacts/xray-core /opt/xray-core

RUN set -eux; \
    case "$TARGETARCH" in amd64) rid=linux-x64; xray_arch=linux-x64 ;; arm64) rid=linux-arm64; xray_arch=linux-arm64 ;; *) exit 1 ;; esac; \
    agent="/opt/frogplug-agent/$rid/FrogPlug.Agent"; \
    test -f "$agent"; \
    install -Dm0755 "$agent" /usr/local/bin/frogplug-agent; \
    archive="$(find "/opt/xray-core/$XRAY_VERSION/$xray_arch" -name '*.zip' -print -quit)"; \
    test -n "$archive"; \
    echo "$(cat "$archive.sha256")  $archive" | sha256sum -c -; \
    unzip -p "$archive" xray > /usr/local/bin/xray; \
    chmod 0755 /usr/local/bin/xray; \
    useradd --system --uid 10001 --home-dir /var/lib/frogplug --create-home frogplug; \
    setcap cap_net_bind_service=+ep /usr/sbin/haproxy; setcap cap_net_bind_service=+ep /usr/local/bin/caddy; setcap cap_net_bind_service=+ep /usr/local/bin/hysteria; \
    mkdir -p /etc/haproxy /etc/caddy /etc/xray /etc/hysteria /etc/frogplug-agent /etc/openflux-node /etc/tproxy-server /etc/mtproxy /etc/supervisor/conf.d /var/lib/frogplug /var/lib/openflux-node; \
    cp /usr/share/mtproxy-bootstrap/proxy-secret /etc/mtproxy/proxy-secret; \
    cp /usr/share/mtproxy-bootstrap/proxy-multi.conf /etc/mtproxy/proxy-multi.conf; \
    echo "dc977517e8a064aae2d26d6a6d9dac6ddeff22e6e19edff87c8f6417d3454c0f  /etc/mtproxy/proxy-secret" | sha256sum -c -; \
    echo "196195d921f16e7815cabf9b843200d7edf1e8219ca2a99c38d393693f5c3c21  /etc/mtproxy/proxy-multi.conf" | sha256sum -c -; \
    chown -R frogplug:frogplug /etc/haproxy /etc/caddy /etc/xray /etc/hysteria /etc/frogplug-agent /etc/openflux-node /etc/tproxy-server /etc/mtproxy /etc/supervisor/conf.d /var/lib/frogplug /var/lib/openflux-node

COPY deploy/docker/node/supervisord.conf /etc/supervisor/supervisord.conf
COPY deploy/docker/node/prepare.sh /usr/local/bin/prepare-node
RUN chmod 0755 /usr/local/bin/prepare-node

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://127.0.0.1:9080/health || exit 1

EXPOSE 80 443
ENTRYPOINT ["/usr/local/bin/prepare-node"]
