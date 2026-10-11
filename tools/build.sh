#!/usr/bin/env bash
# Preflight the sheets, build the plugin and package dist/RobloxObbyPeak-<version>.zip
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION=0.1.0
if [ ! -f lib/BepInEx/core/BepInEx.dll ]; then
  # compile-time references only (BepInEx 5.4, LGPL-2.1); not shipped, Melty installs BepInEx for PEAK itself
  mkdir -p lib/tmp && curl -sSL -o lib/tmp/bep.zip https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/BepInEx_win_x64_5.4.23.3.zip
  (cd lib/tmp && unzip -q -o bep.zip) && mkdir -p lib/BepInEx && cp -r lib/tmp/BepInEx/core lib/BepInEx/ && rm -rf lib/tmp
fi
python3 tools/gen.py
dotnet build src/RobloxObbyPeak -c Release -nologo -v q
rm -rf dist && mkdir -p dist/pkg/plugins
cp src/RobloxObbyPeak/bin/Release/netstandard2.1/RobloxObbyPeak.dll README.md LICENSE dist/pkg/plugins/
(cd dist/pkg && zip -q -X -r "../RobloxObbyPeak-$VERSION.zip" .)
sha256sum "dist/RobloxObbyPeak-$VERSION.zip"
