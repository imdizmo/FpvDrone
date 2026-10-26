#!/usr/bin/env bash
# Build and install FpvDrone into the Gale profile.
# The DLL is replaced by rename, never overwritten in place: a running Valheim keeps the old file
# mapped and reads not-yet-run methods from it, so an in-place copy makes buttons throw
# "BadImageFormatException: Method has zero rva". The new build loads on the next game start.
set -euo pipefail
R="$(cd "$(dirname "$0")/.." && pwd)"
VALHEIM="${VALHEIM:-/mnt/games/SteamLibrary/steamapps/common/Valheim}"
DEST="${DEST:-$HOME/.local/share/com.kesomannen.gale/valheim/profiles/Default/BepInEx/plugins/dizmo-mods}"
dotnet build "$R/FpvDrone.csproj" -c Release -v minimal -o "$R/build" -p:ValheimDir="$VALHEIM" | grep -E "error|Build succeeded" | sort -u
# The user plays while I build: never touch the installed mods while the game runs.
if pgrep -f "valheim[.]x86_64" >/dev/null; then echo "Valheim is running: built only, NOT installed (run again after closing the game)"; exit 0; fi
cp "$R/build/FpvDrone.dll" "$DEST/.FpvDrone.dll.new"
mv -f "$DEST/.FpvDrone.dll.new" "$DEST/FpvDrone.dll"
echo "installed"
