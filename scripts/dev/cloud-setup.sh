#!/usr/bin/env bash
# Cloud environment setup script for citybuild: paste its contents (or run it) as the setup step of a cloud session.
# It installs the .NET SDK pinned in global.json when dotnet is missing, enables the repo hooks, and restores the server packages.
set -euo pipefail

if ! command -v dotnet >/dev/null 2>&1; then
  installer="$(mktemp)"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o "$installer"
  bash "$installer" --jsonfile global.json --install-dir "$HOME/.dotnet"
  rm -f "$installer"
  export PATH="$HOME/.dotnet:$PATH"
fi
dotnet --version

bash scripts/dev/setup-hooks.sh
dotnet tool restore
dotnet restore server/CityBuilder.slnx --locked-mode
