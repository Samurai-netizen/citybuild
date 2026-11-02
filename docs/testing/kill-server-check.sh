#!/usr/bin/env bash
# Manual check (M0-56): kills the server with commands on their way, starts it again, sends every command again with the same request id,
# and counts the rows: each command must have counted once. What it proves and how to read it: docs/testing/KILL-SERVER-CHECK.md.
# Usage: bash docs/testing/kill-server-check.sh      (the dev database running, the API's secrets set; a server on 8080 is left alone)
#        bash docs/testing/kill-server-check.sh --clean   removes the check's players (killcheck-...) and only those from the dev database
# Exit code: 0 no duplicate, and at least one command lost its answer after its commit · 1 a check failed · 2 the setup is wrong ·
#            3 no duplicate, but no command was caught between its commit and its answer, so run it again.
set -uo pipefail

root="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "kill-server-check: run this inside the repository." >&2; exit 2; }
cd "$root" || exit 2
port="${KILL_CHECK_PORT:-8090}"
base="http://localhost:$port/api/v1"
count=60
run="$(date +%s)"
work="$(mktemp -d)"
server_pid=""

# Stops the server this script started, whatever way the script ends.
cleanup() {
  [ -n "$server_pid" ] && kill -9 "$server_pid" 2>/dev/null
}
trap cleanup EXIT

# Starts the server itself (no `dotnet run`, whose kill would leave it running), from its build folder where its settings are, on the check's
# own port with the dev login's limit off (each command needs a player), and waits until it is healthy.
start_server() {
  (cd server/src/CityBuilder.Api/bin/Debug/net10.0 && RateLimits__DevLogin__Limit=1000000 ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS="http://localhost:$port" exec ./CityBuilder.Api) >>"$work/server.log" 2>&1 &
  server_pid=$!
  for _ in $(seq 1 100); do
    [ "$(curl -s "http://localhost:$port/health")" = "Healthy" ] && return 0
    sleep 0.2
  done
  echo "kill-server-check: the server did not start; its log: $work/server.log" >&2
  exit 2
}

# Sends player number $1's placement with its stored token and request id; the answer goes to the file $2, the status to $2.status (000 when none came).
send_command() {
  curl -s --max-time 20 -o "$2" -w '%{http_code}' -X POST "$base/commands/place-building" \
    -H "Authorization: Bearer $(cat "$work/token-$1")" -H 'Content-Type: application/json' \
    -d "{\"requestId\":\"$(cat "$work/id-$1")\",\"buildingTypeId\":\"house\",\"x\":0,\"y\":0}" >"$2.status" 2>/dev/null
}

# Runs SQL on the dev database and prints bare values.
sql() {
  docker compose exec -T postgres sh -c 'psql -X -q -t -A -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<<"$1"
}

if [ -z "$(docker compose ps -q postgres 2>/dev/null)" ]; then
  echo "kill-server-check: the dev database is not running: docker compose up -d postgres" >&2
  exit 2
fi
if [ "${1:-}" = "--clean" ]; then
  sql "BEGIN;
       CREATE TEMP TABLE gone AS SELECT p.id AS player_id, c.id AS city_id FROM players p JOIN cities c ON c.player_id = p.id WHERE p.dev_name LIKE 'killcheck-%';
       DELETE FROM ledger_entries WHERE city_id IN (SELECT city_id FROM gone);
       DELETE FROM command_receipts WHERE player_id IN (SELECT player_id FROM gone);
       DELETE FROM city_buildings WHERE city_id IN (SELECT city_id FROM gone);
       DELETE FROM city_inventory WHERE city_id IN (SELECT city_id FROM gone);
       DELETE FROM city_research WHERE city_id IN (SELECT city_id FROM gone);
       DELETE FROM cities WHERE id IN (SELECT city_id FROM gone);
       DELETE FROM players WHERE id IN (SELECT player_id FROM gone);
       COMMIT;" || exit 1
  echo "Left in the dev database: $(sql "SELECT count(*) FROM players WHERE dev_name LIKE 'killcheck-%';") killcheck players, $(sql "SELECT count(*) FROM players;") players in all."
  exit 0
fi
if curl -s -o /dev/null "http://localhost:$port/health"; then
  echo "kill-server-check: port $port is taken; set KILL_CHECK_PORT to a free one." >&2
  exit 2
fi

echo "== Building and starting the server on port $port"
dotnet build server/src/CityBuilder.Api --nologo -v quiet >"$work/build.log" 2>&1 || { echo "kill-server-check: the build failed: $work/build.log" >&2; exit 2; }
start_server

echo "== Logging in $count players (killcheck-$run-N)"
for i in $(seq 1 "$count"); do
  curl -s -X POST "$base/auth/dev-login" -H 'Content-Type: application/json' -d "{\"displayName\":\"killcheck-$run-$i\"}" \
    | sed 's/.*"accessToken":"\([^"]*\)".*/\1/' >"$work/token-$i"
  uuidgen | tr 'A-Z' 'a-z' >"$work/id-$i"
done

# The first commands of a new server are slow; these warm it up on a player of their own, so the kill below meets commands at full speed.
curl -s -X POST "$base/auth/dev-login" -H 'Content-Type: application/json' -d "{\"displayName\":\"killcheck-$run-warm\"}" \
  | sed 's/.*"accessToken":"\([^"]*\)".*/\1/' >"$work/token-warm"
for x in 0 1; do
  curl -s -o /dev/null -X POST "$base/commands/place-building" -H "Authorization: Bearer $(cat "$work/token-warm")" -H 'Content-Type: application/json' \
    -d "{\"requestId\":\"$(uuidgen | tr 'A-Z' 'a-z')\",\"buildingTypeId\":\"house\",\"x\":$x,\"y\":0}"
done

echo "== Sending every command, then killing the server with them on their way"
pids=()
for i in $(seq 1 "$count"); do
  send_command "$i" "$work/first-$i" &
  pids+=($!)
done

# Kills at the first few answers: the server is at full work then, with commands before, inside and just after their commit.
until [ "$(cat "$work"/first-*.status 2>/dev/null | wc -c)" -ge 15 ]; do :; done
kill -9 "$server_pid"
server_pid=""
wait "${pids[@]}"

where="p.dev_name LIKE 'killcheck-$run-%' AND p.dev_name <> 'killcheck-$run-warm'"
answered=0
for i in $(seq 1 "$count"); do [ "$(cat "$work/first-$i.status")" = "200" ] && answered=$((answered + 1)); done
applied="$(sql "SELECT count(*) FROM command_receipts r JOIN players p ON p.id = r.player_id WHERE $where;")"
caught=$((applied - answered))
echo "   answered 200: $answered · applied in the database: $applied · applied with the answer lost: $caught · not applied: $((count - applied))"

echo "== Starting the server again and sending every command again with the same request id"
start_server
failed=0
pids=()
for i in $(seq 1 "$count"); do
  send_command "$i" "$work/retry-$i" &
  pids+=($!)
done
wait "${pids[@]}"
for i in $(seq 1 "$count"); do
  if [ "$(cat "$work/retry-$i.status")" != "200" ]; then
    echo "FAILED: player $i's retry answered $(cat "$work/retry-$i.status"): $(cat "$work/retry-$i")"
    failed=1
  elif [ "$(cat "$work/first-$i.status")" = "200" ] && ! cmp -s "$work/first-$i" "$work/retry-$i"; then
    echo "FAILED: player $i's retry is not the bytes of the first answer"
    failed=1
  fi
done

echo "== Counting the rows: buildings | COST_PLACE rows | receipts | coins | players"
counts="$(sql "SELECT (SELECT count(*) FROM city_buildings b WHERE b.city_id = c.id) AS buildings,
                      (SELECT count(*) FROM ledger_entries l WHERE l.city_id = c.id AND l.reason = 'COST_PLACE') AS costs,
                      (SELECT count(*) FROM command_receipts r WHERE r.player_id = p.id) AS receipts,
                      (SELECT quantity FROM city_inventory i WHERE i.city_id = c.id AND i.item_id = 'coins') AS coins
               FROM players p JOIN cities c ON c.player_id = p.id WHERE $where;" | sort | uniq -c | sed 's/^ *\([0-9]*\) \(.*\)/\2|\1/')"
echo "$counts" | sed 's/^/   /'
if [ "$(echo "$counts" | wc -l | tr -d ' ')" != "1" ]; then
  echo "FAILED: the players' cities differ, so a command did not count the same for all"
  failed=1
fi
case "$counts" in
  "1|"*"|1|"*"|$count") ;;
  *) echo "FAILED: expected one building and one receipt in each of the $count cities"; failed=1 ;;
esac

if [ "$failed" -ne 0 ]; then
  echo "FAILED. The server's log: $work/server.log"
  exit 1
fi
if [ "$caught" -le 0 ]; then
  echo "NO DUPLICATES, but no command was caught between its commit and its answer. Run it again."
  exit 3
fi
echo "OK: no duplicates; commands that lost their answer after their commit: $caught, and their retries got the stored answer."
