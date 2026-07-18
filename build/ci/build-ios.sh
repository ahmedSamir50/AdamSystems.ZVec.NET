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
ZVEC="$NATIVE/external/zvec"

# CI-only patches (not pushed to alibaba/zvec).
apply_zvec_patch() {
  local patch="$ROOT/build/ci/patches/$1"
  git -C "$ZVEC" apply --check "$patch"
  git -C "$ZVEC" apply "$patch"
}
apply_zvec_patch "zvec-ios-static-output-name.patch"
apply_zvec_patch "zvec-lz4-maccatalyst.patch"
apply_zvec_patch "zvec-arrow-maccatalyst.patch"

# Host protoc: iOS-built protoc is killed (SIGKILL) when run on the Mac host.
# Same pattern as Android GLOBAL_CC_PROTOBUF_PROTOC / win-arm64 host protoc.
HOST_PROTOC_DIR="$NATIVE/build-host-protoc-bin"
ensure_host_protoc() {
  local protoc="$HOST_PROTOC_DIR/bin/protoc"
  if [[ -x "$protoc" ]]; then
    echo "$protoc"
    return 0
  fi
  mkdir -p "$HOST_PROTOC_DIR"
  local zip="$HOST_PROTOC_DIR/protoc.zip"
  local url="https://github.com/protocolbuffers/protobuf/releases/download/v21.12/protoc-21.12-osx-aarch_64.zip"
  # Intel Mac CI fallback (rare for current runners).
  if [[ "$(uname -m)" == "x86_64" ]]; then
    url="https://github.com/protocolbuffers/protobuf/releases/download/v21.12/protoc-21.12-osx-x86_64.zip"
  fi
  echo "Downloading host protoc from $url ..."
  curl -fsSL -o "$zip" "$url"
  unzip -qo "$zip" -d "$HOST_PROTOC_DIR"
  if [[ ! -x "$protoc" ]]; then
    echo "Host protoc missing at $protoc" >&2
    exit 1
  fi
  "$protoc" --version
  echo "$protoc"
}
HOST_PROTOC="$(ensure_host_protoc)"

CMAKE_ARGS=(
  -S "$NATIVE"
  -B "$BUILD_DIR"
  -G Ninja
  -DCMAKE_BUILD_TYPE=Release
  -DCMAKE_POLICY_VERSION_MINIMUM=3.5
  -DCMAKE_SYSTEM_NAME="$SYSTEM_NAME"
  -DCMAKE_OSX_ARCHITECTURES="$ARCHS"
  -DCMAKE_OSX_SYSROOT="$SYSROOT"
  -DBUILD_TESTING=OFF
  -DBUILD_TOOLS=OFF
  -DBUILD_EXAMPLES=OFF
  -DBUILD_PYTHON_BINDINGS=OFF
  -DBUILD_C_BINDINGS=ON
  -DGLOBAL_CC_PROTOBUF_PROTOC="$HOST_PROTOC"
)

if [[ "$TARGET" == "maccatalyst-arm64" ]]; then
  CMAKE_ARGS+=(-DCMAKE_OSX_DEPLOYMENT_TARGET=14.0)
  CMAKE_ARGS+=(-DZVEC_MACCATALYST=ON)
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
