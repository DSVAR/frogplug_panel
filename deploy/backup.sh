#!/usr/bin/env bash
set -euo pipefail

data_dir="${FROGPLUG_DATA_DIRECTORY:-/var/lib/frogplug}"
database="$data_dir/frogplug.db"
backup_dir="$data_dir/backups"
[[ -f "$database" ]] || exit 0
install -d -o frogplug -g frogplug -m 0750 "$backup_dir"
backup="$backup_dir/frogplug-$(date -u +%Y%m%d-%H%M%S).db"
sqlite3 "$database" ".backup '$backup'"
find "$backup_dir" -type f -name 'frogplug-*.db' -printf '%T@ %p\n' |
    sort -nr | tail -n +15 | cut -d' ' -f2- | xargs -r rm -f
