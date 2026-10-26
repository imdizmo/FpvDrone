#!/bin/sh
# Build the plugin and the Thunderstore zip: dist/Jatify-FpvDrone-<version>.zip
set -e
R="$(cd "$(dirname "$0")/.." && pwd)"
V=$(python3 -c "import json;print(json.load(open('$R/package/manifest.json'))['version_number'])")
dotnet build "$R/FpvDrone.csproj" -c Release -v minimal -o "$R/build" -p:ValheimDir="${VALHEIM:-/mnt/games/SteamLibrary/steamapps/common/Valheim}" "$@" >/dev/null
S=$(mktemp -d); trap 'rm -rf "$S"' EXIT
mkdir -p "$S/plugins" "$R/dist"
cp "$R/package/manifest.json" "$R/package/icon.png" "$R/package/README.md" "$R/package/CHANGELOG.md" "$S/"
cp "$R/build/FpvDrone.dll" "$S/plugins/"
cp "$R/LICENSE" "$S/"
rm -f "$R/dist/Jatify-FpvDrone-$V.zip"
(cd "$S" && zip -qr -X "$R/dist/Jatify-FpvDrone-$V.zip" .)
echo "$R/dist/Jatify-FpvDrone-$V.zip"
