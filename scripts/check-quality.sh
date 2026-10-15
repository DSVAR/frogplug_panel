#!/usr/bin/env bash
# Проверка качества кода FrogPlug. Использование: bash scripts/check-quality.sh [минимум_тестов]
# Падает, если код «сжат» в длинные строки, сборка или тесты не проходят, тестов меньше минимума.
set -u
cd "$(dirname "$0")/.."
min_tests="${1:-0}"
fail=0

long=$(LC_ALL=C.UTF-8 grep -rnE '.{161,}' --include=*.cs --include=*.razor --include=*.css src tests \
  | grep -v '/obj/' | grep -v '/bin/' | grep -v '/Migrations/' | grep -v '/wwwroot/lib/' | grep -v 'fonts.css' || true)
if [ -n "$long" ]; then
  echo "FAIL: строки длиннее 160 символов (нужно форматировать нормально):"
  echo "$long" | cut -c1-200 | head -20
  fail=1
fi

dense=$(grep -rnE ';\s*(var|await|return|if|foreach|using) ' --include=*.cs --include=*.razor src \
  | grep -v '/obj/' | grep -v '/Migrations/' | grep -vE 'for\s*\(' || true)
if [ -n "$dense" ]; then
  echo "FAIL: несколько операторов в одной строке:"
  echo "$dense" | cut -c1-200 | head -20
  fail=1
fi

bad_enc=""
for f in $(find src tests docs scripts -type f \( -name '*.cs' -o -name '*.razor' -o -name '*.md' -o -name '*.css' -o -name '*.js' -o -name '*.sh' \)   -not -path '*/obj/*' -not -path '*/bin/*'); do
  iconv -f utf-8 -t utf-8 "$f" > /dev/null 2>&1 || bad_enc="$bad_enc $f"
done
if [ -n "$bad_enc" ]; then
  echo "FAIL: файлы не в UTF-8 (сохраняй русский текст только в UTF-8):$bad_enc"
  fail=1
fi

# Двойная перекодировка: UTF-8, ошибочно прочитанный как cp1251 и сохранённый заново.
mojibake=$(LC_ALL=C.UTF-8 grep -rlE 'Р[°µѕёЅ°ґ]Р|С[ЃЂ‚Џ][Р С]' --include=*.cs --include=*.razor --include=*.md --include=*.sh src tests docs scripts deploy 2>/dev/null | grep -v '/obj/\|/bin/' || true)
if [ -n "$mojibake" ]; then
  echo "FAIL: испорченная кодировка (кракозябры) в файлах: $mojibake"
  fail=1
fi

log_dir="${TMPDIR:-/tmp}"
if ! dotnet build FrogPlug.sln -v q -nologo > "$log_dir/fp-build.log" 2>&1; then
  echo "FAIL: сборка"
  grep -E ' error ' "$log_dir/fp-build.log" | head -20
  fail=1
fi
if grep -qE 'warning NU190[0-9]' "$log_dir/fp-build.log"; then
  echo "FAIL: уязвимые пакеты (NU190x)"
  grep -oE 'warning NU190[0-9][^[]*' "$log_dir/fp-build.log" | sort -u | head
  fail=1
fi

total=0
if DOTNET_CLI_UI_LANGUAGE=en dotnet test FrogPlug.sln --no-build -v q -nologo > "$log_dir/fp-test.log" 2>&1; then
  total=$(grep -oiE 'total: *[0-9]+' "$log_dir/fp-test.log" | grep -oE '[0-9]+' | awk '{s+=$1} END {print s+0}')
  if [ "$total" -lt "$min_tests" ]; then
    echo "FAIL: тестов $total, нужно не меньше $min_tests"
    fail=1
  fi
else
  echo "FAIL: тесты"
  tail -30 "$log_dir/fp-test.log"
  fail=1
fi

[ $fail -eq 0 ] && echo "OK (тестов: $total)"
exit $fail
