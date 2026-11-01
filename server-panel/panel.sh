#!/usr/bin/env bash
# Server panel: starts and stops the local dev database and API for testing the Unity client (docs/testing/LOCAL-E2E.md).
# Usage: panel.sh [start|stop|restart|status|logs|stop-all]   (no argument opens the menu)
#        panel.sh goods show | add <item> <amount> | remove <item> <amount> | set <item> <amount>   (the stock of the player the Unity client uses, or of $PLAYER; CR-0006)
set -u

PANEL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$PANEL_DIR/.." && pwd)"
RUN_DIR="$PANEL_DIR/.run"
PID_FILE="$RUN_DIR/api.pid"
LOG_FILE="$RUN_DIR/api.log"
PORT="${PORT:-8080}"
HEALTH_URL="http://localhost:$PORT/health"
CONTENT_FILE="$ROOT/server/content/mvp-content.json"
GOODS_SQL="$PANEL_DIR/goods.sql"
CLIENT_SETTINGS="$ROOT/client/CityBuilder/Assets/_Project/Resources/Config/ApiClientSettings.asset"

mkdir -p "$RUN_DIR"
cd "$ROOT" || exit 1

# Finds the Docker and dotnet binaries even when the shell was started without the user's profile (double-click launch).
export PATH="$PATH:/usr/local/bin:/opt/homebrew/bin:/usr/local/share/dotnet:$HOME/.dotnet:/Applications/Docker.app/Contents/Resources/bin"

# Prints its arguments as one line of output.
say() { printf '%s\n' "$*"; }

# Prints the process ids that listen on the API port.
port_pids() { lsof -ti "tcp:$PORT" -sTCP:LISTEN 2>/dev/null; }

# True when the API answers its health check.
api_healthy() { [ "$(curl -fsS -m 2 "$HEALTH_URL" 2>/dev/null)" = "Healthy" ]; }

# True when the Postgres container of docker-compose.yml is running.
db_running() { [ -n "$(docker compose ps --status running -q postgres 2>/dev/null)" ]; }

# Shows what is running right now.
status() {
  if db_running; then say "Database : running"; else say "Database : stopped"; fi
  if api_healthy; then say "API      : running and Healthy ($HEALTH_URL)"
  elif [ -n "$(port_pids)" ]; then say "API      : port $PORT is in use, but the health check fails (starting, or another program)"
  else say "API      : stopped"; fi
}

# Starts the database, then the API in the background, and waits until the API is Healthy.
start() {
  if api_healthy; then say "The API is already running."; return 0; fi
  if [ -n "$(port_pids)" ]; then say "Port $PORT is used by another program (pid $(port_pids | tr '\n' ' ')). Stop it first."; return 1; fi
  if ! docker info >/dev/null 2>&1; then say "Docker is not running. Start Docker Desktop and try again."; return 1; fi
  if [ ! -f .env ]; then say "No .env file. Run: cp .env.example .env   (then choose your own password)"; return 1; fi

  say "Starting the database..."
  docker compose up -d postgres >/dev/null || { say "The database did not start."; return 1; }

  say "Starting the API (log: $LOG_FILE)..."
  : > "$LOG_FILE"
  nohup dotnet run --project server/src/CityBuilder.Api >>"$LOG_FILE" 2>&1 &
  echo $! > "$PID_FILE"

  local i
  for i in $(seq 1 90); do
    if api_healthy; then say "Ready: the API is Healthy at $HEALTH_URL"; return 0; fi
    if ! kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
      say "The API stopped while starting. Last log lines:"; tail -n 15 "$LOG_FILE"; return 1
    fi
    sleep 1
  done
  say "The API did not become Healthy within 90 s. Check the log (option: logs)."
  return 1
}

# Stops the API: the dotnet run process, its children, and whatever still listens on the port.
stop() {
  local pids=""
  if [ -f "$PID_FILE" ]; then
    local parent; parent="$(cat "$PID_FILE")"
    pids="$parent $(pgrep -P "$parent" 2>/dev/null | tr '\n' ' ')"
  fi
  pids="$pids $(port_pids | tr '\n' ' ')"
  pids="$(echo "$pids" | tr ' ' '\n' | sort -u | grep -E '^[0-9]+$' | tr '\n' ' ')"
  rm -f "$PID_FILE"

  if [ -z "$pids" ]; then say "The API is not running."; return 0; fi
  say "Stopping the API (pid $pids)..."
  # shellcheck disable=SC2086
  kill $pids 2>/dev/null
  local i
  for i in $(seq 1 10); do
    [ -z "$(port_pids)" ] && { say "The API is stopped."; return 0; }
    sleep 1
  done
  # shellcheck disable=SC2086
  kill -9 $pids 2>/dev/null
  sleep 1
  if [ -z "$(port_pids)" ]; then say "The API is stopped (forced)."; else say "Could not stop the process on port $PORT."; return 1; fi
}

# Stops the API and the database container (the data stays in its volume).
stop_all() {
  stop
  say "Stopping the database..."
  docker compose stop postgres >/dev/null 2>&1 && say "The database is stopped." || say "The database was not running."
}

# Shows the end of the API log.
logs() {
  if [ -f "$LOG_FILE" ]; then tail -n 40 "$LOG_FILE"; else say "No log yet."; fi
}

# Prints the dev name of the player the goods commands work on: $PLAYER if set, else the name the Unity client logs in as
# (devDisplayName in ApiClientSettings.asset), so the stock changes in the city you are looking at. Fails with a message for an invalid name.
goods_player() {
  local name="${PLAYER:-}"
  [ -z "$name" ] && name="$(sed -n 's/^ *devDisplayName: *//p' "$CLIENT_SETTINGS" 2>/dev/null | head -n 1 | tr -d '\r"')"
  if [ -z "$name" ]; then say "No player: set PLAYER=<dev name>, or the client's $CLIENT_SETTINGS has no devDisplayName."; return 1; fi
  if ! [[ "$name" =~ ^[A-Za-z0-9_.-]{1,32}$ ]]; then say "'$name' is not a valid dev name (1 to 32 letters, digits, _ - .)."; return 1; fi
  printf '%s\n' "$name"
}

# Prints the item ids of the content file, one per line (the only list of items; no id is written in this script).
item_ids() {
  python3 -c 'import json,sys; print("\n".join(i["id"] for i in json.load(open(sys.argv[1]))["items"]))' "$CONTENT_FILE" 2>/dev/null
}

# Runs goods.sql for the player $GOODS_PLAYER in the database container. $1 mode, $2 item id ("" for show), $3 amount ("" for show).
# The values travel as arguments of the container's shell, never inside a command line that is built from them.
goods_sql() {
  docker compose exec -T postgres sh -c \
    'psql -X -q -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -v "player=$1" -v "mode=$2" -v "item=$3" -v "amount=$4" -f -' \
    sh "$GOODS_PLAYER" "$1" "${2:-x}" "${3:-0}" < "$GOODS_SQL"
}

# The goods commands: `goods show` or `goods add|remove|set <item> <amount>`. Checks the arguments first, then changes the stock.
goods() {
  local mode="${1:-}" item="${2:-}" amount="${3:-}"
  GOODS_PLAYER="$(goods_player)" || { say "$GOODS_PLAYER"; return 1; }
  case "$mode" in
    show) [ $# -eq 1 ] || { say "Usage: panel.sh goods show"; return 2; } ;;
    add|remove|set) [ $# -eq 3 ] || { say "Usage: panel.sh goods $mode <item> <amount>"; return 2; } ;;
    *) say "Usage: panel.sh goods show | add <item> <amount> | remove <item> <amount> | set <item> <amount>"; return 2 ;;
  esac
  if [ "$mode" != "show" ]; then
    local ids; ids="$(item_ids)" || ids=""
    if [ -z "$ids" ]; then say "Could not read the item ids from $CONTENT_FILE (python3 is needed)."; return 1; fi
    if ! printf '%s\n' "$ids" | grep -qxF -- "$item"; then
      say "Unknown item '$item'. Items: $(printf '%s' "$ids" | tr '\n' ' ')"; return 2
    fi
    if ! [[ "$amount" =~ ^[0-9]{1,12}$ ]]; then say "The amount must be a whole number from 0 to 999999999999."; return 2; fi
  fi
  if ! db_running; then say "The database is not running. Start it first (option 1 of the menu, or: panel.sh start)."; return 1; fi
  say "Player: $GOODS_PLAYER"
  local out; out="$(goods_sql "$mode" "$item" "$amount" 2>&1)"; local code=$?
  say "$out"
  [ $code -eq 0 ] && ! printf '%s\n' "$out" | grep -q '^REFUSED' && return 0
  return 1
}

# The goods part of the menu: shows the stock, then asks for an item, add / remove / set and an amount.
goods_menu() {
  local ids i=0 item choice mode amount
  ids="$(item_ids)" || ids=""
  [ -z "$ids" ] && { say "Could not read the item ids from $CONTENT_FILE (python3 is needed)."; return 1; }
  goods show || return 1
  say ""
  while read -r item; do i=$((i + 1)); say " $i) $item"; done <<< "$ids"
  read -r -p "Item number (Enter to go back): " choice
  [ -z "$choice" ] && return 0
  [[ "$choice" =~ ^[0-9]+$ ]] || { say "Unknown choice."; return 1; }
  item="$(printf '%s\n' "$ids" | sed -n "${choice}p" 2>/dev/null)"
  [ -z "$item" ] && { say "Unknown choice."; return 1; }
  read -r -p "a = add, r = remove, s = set: " mode
  case "$mode" in a) mode=add ;; r) mode=remove ;; s) mode=set ;; *) say "Unknown choice."; return 1 ;; esac
  read -r -p "Amount: " amount
  goods "$mode" "$item" "$amount"
}

# The interactive menu.
menu() {
  while true; do
    clear
    say "=== CityBuilder server panel ==="
    status
    say ""
    say " 1) Start    (database + API)"
    say " 2) Stop     (API)"
    say " 3) Restart  (API)"
    say " 4) Log      (last 40 lines)"
    say " 5) Stop all (API + database)"
    say " 6) Goods     (add or remove items of the player "$(goods_player 2>/dev/null || echo '?')")"
    say " 0) Quit     (the server keeps running)"
    say ""
    read -r -p "Choose: " choice
    say ""
    case "$choice" in
      1) start ;;
      2) stop ;;
      3) stop; start ;;
      4) logs ;;
      5) stop_all ;;
      6) goods_menu ;;
      0|q|Q) exit 0 ;;
      *) say "Unknown choice." ;;
    esac
    say ""
    read -r -p "Press Enter to continue..." _
  done
}

case "${1:-menu}" in
  start) start ;;
  stop) stop ;;
  restart) stop; start ;;
  status) status ;;
  logs) logs ;;
  stop-all) stop_all ;;
  goods) shift; goods "$@" ;;
  menu) menu ;;
  *) say "Usage: panel.sh [start|stop|restart|status|logs|stop-all|goods ...]"; exit 2 ;;
esac
