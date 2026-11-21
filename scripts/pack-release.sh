#!/usr/bin/env bash
# Packs the release packages into artifacts/packages, after a Release build.
# With no argument: the libraries, the CLI's pointer package and its framework-dependent fallback.
# With a runtime identifier: the native CLI package for that runtime, on a matching system.
# Usage: scripts/pack-release.sh [runtime-identifier]
set -euo pipefail

cd "$(dirname "$0")/.."
TOOL_RUNTIMES="linux-x64;linux-arm64;osx-arm64;win-x64;win-arm64;any"
OUT=artifacts/packages

pack_cli() {
  dotnet pack src/Ahr.Assurance.Cli -c Release -o "$OUT" "-p:ToolPackageRuntimeIdentifiers=\"$TOOL_RUNTIMES\"" "$@"
}

if [ $# -eq 1 ]; then
  pack_cli -r "$1"
  exit 0
fi

dotnet pack Ahr.Assurance.slnx -c Release --no-build -o "$OUT"
rm -f "$OUT"/Ahr.Assurance.Cli.*
# The pointer package holds no assemblies, so it has no symbols package.
pack_cli -p:IncludeSymbols=false
pack_cli -r any -p:PublishAot=false
for package in Ahr.Assurance.Cli Ahr.Assurance.Cli.any; do
  ls "$OUT/$package".[0-9]*.nupkg > /dev/null
done
