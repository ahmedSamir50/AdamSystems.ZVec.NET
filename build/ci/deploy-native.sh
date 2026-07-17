#!/usr/bin/env bash
# Copy a built zvec_c_api shared library into src/Core/ZVec.NET/runtimes/{rid}/native/
set -euo pipefail

RID="${1:?Usage: deploy-native.sh <rid> <path-to-native-lib>}"
SRC="${2:?Usage: deploy-native.sh <rid> <path-to-native-lib>}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DEST_DIR="$ROOT/src/Core/ZVec.NET/runtimes/$RID/native"

if [[ ! -f "$SRC" ]]; then
  echo "Native library not found: $SRC" >&2
  exit 1
fi

mkdir -p "$DEST_DIR"
cp -f "$SRC" "$DEST_DIR/"
echo "Deployed $(basename "$SRC") -> $DEST_DIR/"
ls -la "$DEST_DIR"
