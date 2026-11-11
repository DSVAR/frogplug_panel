#!/usr/bin/env bash
set -euo pipefail

cat > /etc/supervisor/conf.d/mtg.conf <<'EOF'
[program:mtg]
command=/usr/local/bin/mtg run /etc/mtg.toml
user=frogplug
priority=35
autostart=false
autorestart=false
startretries=3
EOF

cat > /etc/supervisor/conf.d/hysteria.conf <<'EOF'
[program:hysteria]
command=/usr/local/bin/hysteria server -c /etc/hysteria/config.yaml
user=frogplug
priority=35
autostart=false
autorestart=false
startretries=3
EOF


exec /usr/bin/supervisord -n -c /etc/supervisor/supervisord.conf
