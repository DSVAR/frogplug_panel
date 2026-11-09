FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
ARG TARGETARCH
COPY . .
RUN set -eux; \
    case "$TARGETARCH" in amd64) rid=linux-x64 ;; arm64) rid=linux-arm64 ;; *) exit 1 ;; esac; \
    dotnet publish src/FrogPlug.Server/FrogPlug.Server.csproj \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o /out

FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl sqlite3 libicu72 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /out /opt/panel
COPY artifacts/xray-core /opt/frogplug/xray-core

# Внутри контейнера слушаем все интерфейсы; наружу порт публикуется только на 127.0.0.1 хоста.
ENV ASPNETCORE_URLS=http://0.0.0.0:5280 \
    DataDirectory=/data \
    XrayCore__BundledPath=/opt/frogplug/xray-core

RUN chmod 0755 /opt/panel/FrogPlug.Server \
    && ln -s /opt/panel/FrogPlug.Server /usr/local/bin/FrogPlug.Server \
    && useradd --system --uid 10001 --home-dir /data --create-home frogplug \
    && mkdir -p /data \
    && chown -R frogplug:frogplug /data

VOLUME ["/data"]
EXPOSE 5280
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://127.0.0.1:5280/health || exit 1
USER frogplug
WORKDIR /opt/panel
ENTRYPOINT ["/opt/panel/FrogPlug.Server"]
