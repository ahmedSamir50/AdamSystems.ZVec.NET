#!/usr/bin/env bash
# Build zvec_c_api for iOS / iOS Simulator / Mac Catalyst (must run on macOS with Xcode).
set -euo pipefail

TARGET="${1:?Usage: build-ios.sh <ios-arm64|iossimulator-arm64|maccatalyst-arm64>}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NATIVE="$ROOT/src/Native/ZVec.Native"

case "$TARGET" in
  ios-arm64)
    RID=ios-arm64
    SYSTEM_NAME=iOS
    ARCHS=arm64
    SDK=iphoneos
    ;;
  iossimulator-arm64)
    RID=iossimulator-arm64
    SYSTEM_NAME=iOS
    ARCHS=arm64
    SDK=iphonesimulator
    ;;
  maccatalyst-arm64)
    RID=maccatalyst-arm64
    SYSTEM_NAME=Darwin
    ARCHS=arm64
    SDK=macosx
    ;;
  *)
    echo "Unsupported target: $TARGET" >&2
    exit 1
    ;;
esac

BUILD_DIR="$NATIVE/build-$RID"
SYSROOT="$(xcrun --sdk "$SDK" --show-sdk-path)"

CMAKE_ARGS=(
  -S "$NATIVE"
  -B "$BUILD_DIR"
  -G Ninja
  -DCMAKE_BUILD_TYPE=Release
  -DCMAKE_SYSTEM_NAME="$SYSTEM_NAME"
  -DCMAKE_OSX_ARCHITECTURES="$ARCHS"
  -DCMAKE_OSX_SYSROOT="$SYSROOT"
  -DBUILD_TESTING=OFF
  -DBUILD_TOOLS=OFF
  -DBUILD_EXAMPLES=OFF
  -DBUILD_PYTHON_BINDINGS=OFF
  -DBUILD_C_BINDINGS=ON
)

if [[ "$TARGET" == "maccatalyst-arm64" ]]; then
  CMAKE_ARGS+=(-DCMAKE_OSX_DEPLOYMENT_TARGET=14.0)
  # Catalyst: prefer iOS macabi when available
  CMAKE_ARGS+=(-DCMAKE_CXX_FLAGS="-target arm64-apple-ios14.0-macabi")
  CMAKE_ARGS+=(-DCMAKE_C_FLAGS="-target arm64-apple-ios14.0-macabi")
elif [[ "$TARGET" == ios-arm64 ]]; then
  CMAKE_ARGS+=(-DCMAKE_OSX_DEPLOYMENT_TARGET=15.0)
elif [[ "$TARGET" == iossimulator-arm64 ]]; then
  CMAKE_ARGS+=(-DCMAKE_OSX_DEPLOYMENT_TARGET=15.0)
fi

cmake "${CMAKE_ARGS[@]}"
cmake --build "$BUILD_DIR" --config Release --target zvec_c_api -j"$(sysctl -n hw.ncpu)"

LIB="$(find "$BUILD_DIR" \( -name 'libzvec_c_api.dylib' -o -name 'zvec_c_api.dylib' \) | head -n1)"
if [[ -z "$LIB" ]]; then
  # Some iOS configs produce .so-named or framework; also accept .a for static — we need shared.
  echo "Could not find libzvec_c_api.dylib under $BUILD_DIR" >&2
  find "$BUILD_DIR" \( -name '*.dylib' -o -name '*.so' \) | head -50 >&2 || true
  exit 1
fi

"$ROOT/build/ci/deploy-native.sh" "$RID" "$LIB"
