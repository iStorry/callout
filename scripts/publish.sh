#!/usr/bin/env bash
# Builds self-contained Callout packages for every supported Windows architecture.
# Usage: scripts/publish.sh [version]   e.g. scripts/publish.sh 0.1.0
set -euo pipefail

readonly ARCHITECTURES=(win-x64 win-arm64)
readonly ROOT="$(cd "$(dirname "$0")/.." && pwd)"
readonly OUT="$ROOT/artifacts"
readonly VERSION="${1:-}"

rm -rf "$OUT"
for rid in "${ARCHITECTURES[@]}"; do
  dir="$OUT/Callout-$rid"
  dotnet publish "$ROOT/src/Callout" -c Release -r "$rid" -o "$dir" ${VERSION:+"-p:Version=$VERSION"}
  (cd "$dir" && zip -qr "$OUT/Callout-$rid.zip" .)
  echo "Packaged $OUT/Callout-$rid.zip"
done
