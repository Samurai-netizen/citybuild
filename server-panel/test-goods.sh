#!/usr/bin/env bash
# Tests of the goods commands of panel.sh (CR-0006) that need no database: the arguments are checked before anything is run, and a valid
# command reaches the database container with exactly the player (the one the Unity client logs in as, or $PLAYER), the mode, the item and the amount. A fake `docker` stands in for
# the real one. The SQL itself is tested against PostgreSQL by GoodsPanelScriptTests (docs/testing/README.md).
# Usage: bash server-panel/test-goods.sh   (exit 0 when every case passes)
set -u

PANEL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
FAILED=0

# A fake docker: `compose ps` answers like a running container, `compose exec` records its arguments and answers like goods.sql.
mkdir -p "$WORK/bin"
cat > "$WORK/bin/docker" <<'FAKE'
#!/usr/bin/env bash
case " $* " in
  *" ps "*) echo "fake-container" ;;
  *" exec "*) printf '%s\n' "$*" >> "$FAKE_DOCKER_LOG"; cat > /dev/null; echo "OK fake 1 -> 2" ;;
esac
FAKE
chmod +x "$WORK/bin/docker"
export FAKE_DOCKER_LOG="$WORK/exec.log"

# Runs panel.sh goods with the given arguments against the fake docker; sets OUT and CODE, and clears the exec log.
run_goods() {
  : > "$FAKE_DOCKER_LOG"
  OUT="$(PATH="$WORK/bin:$PATH" bash "$PANEL_DIR/panel.sh" goods "$@" 2>&1)"; CODE=$?
}

# Records a failure of the named case.
fail() { echo "FAIL: $1"; FAILED=1; }

# A command that must be refused: non-zero exit, the message contains $2, and docker never ran `exec`.
expect_refused() {
  local name="$1" text="$2"; shift 2
  run_goods "$@"
  [ "$CODE" -ne 0 ] || fail "$name: exit code was 0"
  case "$OUT" in *"$text"*) ;; *) fail "$name: message '$OUT' lacks '$text'" ;; esac
  [ ! -s "$FAKE_DOCKER_LOG" ] || fail "$name: the database was reached"
}

expect_refused "no mode" "Usage" 
expect_refused "unknown mode" "Usage" give planks 5
expect_refused "add without amount" "Usage: panel.sh goods add" add planks
expect_refused "show with arguments" "Usage: panel.sh goods show" show planks
expect_refused "unknown item" "Unknown item" add gold 5
expect_refused "item with a quote" "Unknown item" add "planks'; drop table cities; --" 5
expect_refused "negative amount" "whole number" add planks -5
expect_refused "text amount" "whole number" add planks five
expect_refused "amount too big" "whole number" add planks 1000000000000

# A name that cannot be a dev name is refused.
PLAYER='bad name; drop' expect_refused "bad PLAYER" "not a valid dev name" add planks 5

# Without $PLAYER the command works on the player the Unity client logs in as.
CLIENT_PLAYER="$(sed -n 's/^ *devDisplayName: *//p' "$PANEL_DIR/../client/CityBuilder/Assets/_Project/Resources/Config/ApiClientSettings.asset" | head -n 1 | tr -d '\r"')"
[ -n "$CLIENT_PLAYER" ] || fail "the client settings have no devDisplayName"
run_goods add planks 50
[ "$CODE" -eq 0 ] || fail "valid add: exit code $CODE ($OUT)"
case "$OUT" in *"Player: $CLIENT_PLAYER"*) ;; *) fail "valid add: the output does not name the player '$CLIENT_PLAYER': $OUT" ;; esac
case "$(cat "$FAKE_DOCKER_LOG")" in *"sh $CLIENT_PLAYER add planks 50"*) ;; *) fail "valid add: arguments were '$(cat "$FAKE_DOCKER_LOG")'" ;; esac
[ "$(wc -l < "$FAKE_DOCKER_LOG" | tr -d ' ')" = "1" ] || fail "valid add: docker exec ran more than once"

PLAYER=someone run_goods remove logs 3
[ "$CODE" -eq 0 ] || fail "PLAYER: exit code $CODE"
case "$(cat "$FAKE_DOCKER_LOG")" in *"sh someone remove logs 3"*) ;; *) fail "PLAYER: arguments were '$(cat "$FAKE_DOCKER_LOG")'" ;; esac

run_goods set logs 0
[ "$CODE" -eq 0 ] || fail "set 0: exit code $CODE"
run_goods show
[ "$CODE" -eq 0 ] || fail "show: exit code $CODE"
case "$(cat "$FAKE_DOCKER_LOG")" in *"sh $CLIENT_PLAYER show"*) ;; *) fail "show: arguments were '$(cat "$FAKE_DOCKER_LOG")'" ;; esac

if [ "$FAILED" -eq 0 ]; then echo "test-goods: OK"; else echo "test-goods: FAILED"; exit 1; fi
