#!/usr/bin/env bash
# Builds self-contained Callout packages: a folder and a portable zip per Windows architecture.
# Usage: scripts/publish.sh [version] [runtime-id...]
#   scripts/publish.sh                    all architectures, version from the project file
#   scripts/publish.sh 0.2.0 win-x64      one architecture, stamped 0.2.0
set -euo pipefail

readonly ROOT="$(cd "$(dirname "$0")/.." && pwd)"
readonly OUT="$ROOT/artifacts"
readonly VERSION="${1:-}"
if (($# > 1)); then
  readonly ARCHITECTURES=("${@:2}")
else
  readonly ARCHITECTURES=(win-x64 win-arm64)
fi

rm -rf "$OUT"
for rid in "${ARCHITECTURES[@]}"; do
  dir="$OUT/Callout-$rid"
  dotnet publish "$ROOT/src/Callout" -c Release -r "$rid" -o "$dir" ${VERSION:+"-p:Version=$VERSION"}
  (cd "$dir" && zip -qr "$OUT/Callout-$rid.zip" .)
  echo "Packaged $OUT/Callout-$rid.zip"
done
