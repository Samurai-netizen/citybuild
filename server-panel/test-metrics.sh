#!/usr/bin/env bash
# Tests of `panel.sh metrics` (CR-0009-T2) that need no database: the panel refuses before running anything when its inputs are missing,
# and a valid call runs the report tool with the page path the panel owns, the user's arguments unchanged, and the connection string in
# the environment (never in the arguments: the process list shows those); then opens the page. A fake `docker`, `dotnet`, `open` and
# `xdg-open` stand in for the real ones, and a fake .env (ENV_FILE) for the real one. The tool itself is tested by tools/MetricsReport.Tests
# and the integration project (docs/testing/README.md).
# Usage: bash server-panel/test-metrics.sh   (exit 0 when every case passes)
set -u

PANEL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PAGE="$PANEL_DIR/.run/metrics/report.html"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
FAILED=0

# A fake docker: `compose ps` answers like a running container, or like none when FAKE_DB_DOWN is set.
mkdir -p "$WORK/bin"
cat > "$WORK/bin/docker" <<'FAKE'
#!/usr/bin/env bash
case " $* " in
  *" ps "*) [ -n "${FAKE_DB_DOWN:-}" ] || echo "fake-container" ;;
esac
FAKE
# A fake dotnet: records its arguments and the connection string, prints what the tool prints, exits with FAKE_DOTNET_EXIT (default 0).
cat > "$WORK/bin/dotnet" <<'FAKE'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$FAKE_DOTNET_LOG"
printf '%s\n' "${ConnectionStrings__CityBuilder:-}" >> "$FAKE_CONNECTION_LOG"
echo "fake report"
exit "${FAKE_DOTNET_EXIT:-0}"
FAKE
# A fake opener (both names): records the file it was asked to open.
cat > "$WORK/bin/open" <<'FAKE'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$FAKE_OPEN_LOG"
FAKE
cp "$WORK/bin/open" "$WORK/bin/xdg-open"
chmod +x "$WORK/bin/docker" "$WORK/bin/dotnet" "$WORK/bin/open" "$WORK/bin/xdg-open"
export FAKE_DOTNET_LOG="$WORK/dotnet.log" FAKE_CONNECTION_LOG="$WORK/connection.log" FAKE_OPEN_LOG="$WORK/open.log"

# The fake .env: a password with a ; and a ' (both must survive into the connection string), another port.
cat > "$WORK/env" <<'ENV'
# a comment
POSTGRES_USER=u1
POSTGRES_PASSWORD=p;w'd
POSTGRES_DB=db1
POSTGRES_PORT=5433
ENV
grep -v '^POSTGRES_PORT=' "$WORK/env" > "$WORK/env-no-port"
grep -v '^POSTGRES_PASSWORD=' "$WORK/env" > "$WORK/env-no-password"

# Runs panel.sh metrics with the given arguments against the fakes; sets OUT and CODE, and clears the logs. ENV_FILE defaults to the fake .env.
run_metrics() {
  : > "$FAKE_DOTNET_LOG"; : > "$FAKE_CONNECTION_LOG"; : > "$FAKE_OPEN_LOG"
  OUT="$(ENV_FILE="${ENV_FILE:-$WORK/env}" PATH="$WORK/bin:$PATH" bash "$PANEL_DIR/panel.sh" metrics "$@" 2>&1)"; CODE=$?
}

# Records a failure of the named case.
fail() { echo "FAIL: $1"; FAILED=1; }

# A call that must be refused: non-zero exit, the message contains $2, and dotnet never ran.
expect_refused() {
  local name="$1" text="$2"; shift 2
  run_metrics "$@"
  [ "$CODE" -ne 0 ] || fail "$name: exit code was 0"
  case "$OUT" in *"$text"*) ;; *) fail "$name: message '$OUT' lacks '$text'" ;; esac
  [ ! -s "$FAKE_DOTNET_LOG" ] || fail "$name: the tool ran"
}

expect_refused "own --out" "--out" --out somewhere.html
ENV_FILE="$WORK/missing" expect_refused "no .env" "No .env file"
ENV_FILE="$WORK/env-no-password" expect_refused "no password" "POSTGRES_PASSWORD"
FAKE_DB_DOWN=1 expect_refused "database stopped" "The database is not running"

# A valid call: the page path of the panel, the arguments unchanged, the connection string only in the environment, the page opened.
run_metrics --exclude devtest --from 2026-11-01
[ "$CODE" -eq 0 ] || fail "valid: exit code $CODE ($OUT)"
[ "$(cat "$FAKE_DOTNET_LOG")" = "run --project tools/MetricsReport -- --out $PAGE --exclude devtest --from 2026-11-01" ] \
  || fail "valid: arguments were '$(cat "$FAKE_DOTNET_LOG")'"
[ "$(cat "$FAKE_CONNECTION_LOG")" = "Host=localhost;Port=5433;Database=db1;Username=u1;Password='p;w''d'" ] \
  || fail "valid: connection string was '$(cat "$FAKE_CONNECTION_LOG")'"
case "$(cat "$FAKE_DOTNET_LOG")" in *"p;w"*) fail "valid: the password is in the arguments" ;; esac
[ "$(cat "$FAKE_OPEN_LOG")" = "$PAGE" ] || fail "valid: opened '$(cat "$FAKE_OPEN_LOG")'"
case "$OUT" in *"$PAGE"*) ;; *) fail "valid: the output does not name the page: $OUT" ;; esac

# Without POSTGRES_PORT the port is compose's default.
ENV_FILE="$WORK/env-no-port" run_metrics
[ "$CODE" -eq 0 ] || fail "default port: exit code $CODE ($OUT)"
case "$(cat "$FAKE_CONNECTION_LOG")" in "Host=localhost;Port=5432;"*) ;; *) fail "default port: '$(cat "$FAKE_CONNECTION_LOG")'" ;; esac

# A failed run passes its exit code on and opens nothing.
FAKE_DOTNET_EXIT=2 run_metrics
[ "$CODE" -eq 2 ] || fail "tool fails: exit code $CODE"
[ ! -s "$FAKE_OPEN_LOG" ] || fail "tool fails: the page was opened"

# --help needs neither .env nor the database, and opens nothing.
ENV_FILE="$WORK/missing" FAKE_DB_DOWN=1 run_metrics --help
[ "$CODE" -eq 0 ] || fail "help: exit code $CODE ($OUT)"
[ "$(cat "$FAKE_DOTNET_LOG")" = "run --project tools/MetricsReport -- --help" ] || fail "help: arguments were '$(cat "$FAKE_DOTNET_LOG")'"
[ ! -s "$FAKE_OPEN_LOG" ] || fail "help: something was opened"

if [ "$FAILED" -eq 0 ]; then echo "test-metrics: OK"; else echo "test-metrics: FAILED"; exit 1; fi
