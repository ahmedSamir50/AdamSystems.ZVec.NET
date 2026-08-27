#!/usr/bin/env bash
# Build zvec_c_api for Android via NDK and deploy into runtimes/android-{arm64|x64}/native/
#
# Matches upstream zvec 04-android-build.yml:
#   1) download host protoc (zvec 0.7.0 no longer vendors protobuf / protoc target)
#   2) cross-compile with GLOBAL_CC_PROTOBUF_PROTOC for Arrow ExternalProject
set -euo pipefail

ABI="${1:-arm64-v8a}"
# API ≥ 28 required for std::aligned_alloc (bionic); 34 matches upstream zvec Android CI.
API_LEVEL="${ANDROID_API_LEVEL:-34}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NATIVE="$ROOT/src/Native/ZVec.Native"
ZVEC="$NATIVE/external/zvec"
JOBS="$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 2)"

if [[ -z "${ANDROID_NDK_HOME:-}${ANDROID_NDK_ROOT:-}" ]]; then
  echo "Set ANDROID_NDK_HOME or ANDROID_NDK_ROOT to your NDK path." >&2
  exit 1
fi
NDK="${ANDROID_NDK_HOME:-$ANDROID_NDK_ROOT}"

case "$ABI" in
  arm64-v8a) RID=android-arm64 ;;
  x86_64)    RID=android-x64 ;;
  armeabi-v7a) RID=android-arm ;;
  x86)       RID=android-x86 ;;
  *) echo "Unsupported ANDROID_ABI: $ABI" >&2; exit 1 ;;
esac

HOST_PROTOC_DIR="$NATIVE/build-host-protoc-bin"
BUILD_DIR="$NATIVE/build-android-$ABI"
TOOLCHAIN="$NDK/build/cmake/android.toolchain.cmake"

ensure_host_protoc() {
  local protoc="$HOST_PROTOC_DIR/bin/protoc"
  if [[ -x "$protoc" ]]; then
    echo "$protoc"
    return 0
  fi
  mkdir -p "$HOST_PROTOC_DIR"
  local zip="$HOST_PROTOC_DIR/protoc.zip"
  local url="https://github.com/protocolbuffers/protobuf/releases/download/v21.12/protoc-21.12-linux-x86_64.zip"
  echo "Downloading host protoc from $url ..." >&2
  curl -fsSL -o "$zip" "$url"
  unzip -qo "$zip" -d "$HOST_PROTOC_DIR"
  if [[ ! -x "$protoc" ]]; then
    echo "Host protoc missing at $protoc" >&2
    exit 1
  fi
  "$protoc" --version >&2
  echo "$protoc"
}

HOST_PROTOC="$(ensure_host_protoc)"

echo "Cross-compiling zvec_c_api for ANDROID_ABI=$ABI..."
rm -f "$BUILD_DIR/CMakeCache.txt"

cmake -S "$NATIVE" -B "$BUILD_DIR" \
  -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
  -DOVERRIDE_GIT_DESCRIBE=v0.7.0 \
  -DCMAKE_TOOLCHAIN_FILE="$TOOLCHAIN" \
  -DANDROID_ABI="$ABI" \
  -DANDROID_PLATFORM="android-${API_LEVEL}" \
  -DANDROID_STL=c++_shared \
  -DBUILD_TESTING=OFF \
  -DBUILD_TOOLS=OFF \
  -DBUILD_EXAMPLES=OFF \
  -DBUILD_PYTHON_BINDINGS=OFF \
  -DBUILD_C_BINDINGS=ON \
  -DGLOBAL_CC_PROTOBUF_PROTOC="$HOST_PROTOC"

cmake --build "$BUILD_DIR" --config Release --target zvec_c_api -j"$JOBS"

LIB="$(find "$BUILD_DIR" -name 'libzvec_c_api.so' -o -name 'zvec_c_api.so' | head -n1)"
if [[ -z "$LIB" ]]; then
  echo "Could not find libzvec_c_api.so under $BUILD_DIR" >&2
  find "$BUILD_DIR" -name '*.so' | head -50 >&2 || true
  exit 1
fi

"$ROOT/build/ci/deploy-native.sh" "$RID" "$LIB"
