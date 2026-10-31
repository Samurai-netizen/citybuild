#!/usr/bin/env bash
# Double-click launcher for the server panel: opens the menu of panel.sh in a Terminal window.
exec "$(cd "$(dirname "$0")" && pwd)/panel.sh" menu
