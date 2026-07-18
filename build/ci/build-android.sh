#!/usr/bin/env bash
# Build zvec_c_api for Android via NDK and deploy into runtimes/android-{arm64|x64}/native/
#
# Builds a host (Linux x86_64) protoc first, then cross-compiles with NDK.
# Without GLOBAL_CC_PROTOBUF_PROTOC, NDK-built protoc fails with Exec format error
# when run on the CI host (matches upstream zvec 04-android-build.yml).
set -euo pipefail

ABI="${1:-arm64-v8a}"
# API ≥ 28 required for std::aligned_alloc (bionic); 34 matches upstream zvec Android CI.
API_LEVEL="${ANDROID_API_LEVEL:-34}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NATIVE="$ROOT/src/Native/ZVec.Native"
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

HOST_BUILD="$NATIVE/build-host-protoc"
BUILD_DIR="$NATIVE/build-android-$ABI"
TOOLCHAIN="$NDK/build/cmake/android.toolchain.cmake"

find_host_protoc() {
  # Prefer versioned binary used by zvec's protobuf ExternalProject layout.
  local candidate
  for candidate in \
    "$HOST_BUILD/external/zvec/bin/protoc-3.21.12.0" \
    "$HOST_BUILD/bin/protoc" \
    "$HOST_BUILD/external/zvec/bin/protoc"
  do
    if [[ -x "$candidate" ]]; then
      echo "$candidate"
      return 0
    fi
  done
  candidate="$(find "$HOST_BUILD" -type f \( -name 'protoc-3.21.12.0' -o -name 'protoc' \) -perm -111 2>/dev/null | head -n1 || true)"
  if [[ -n "$candidate" ]]; then
    echo "$candidate"
    return 0
  fi
  return 1
}

HOST_PROTOC=""
if HOST_PROTOC="$(find_host_protoc)"; then
  echo "Using cached host protoc: $HOST_PROTOC"
else
  echo "Building host protoc (no NDK toolchain)..."
  cmake -S "$NATIVE" -B "$HOST_BUILD" \
    -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DBUILD_TESTING=OFF \
    -DBUILD_TOOLS=OFF \
    -DBUILD_EXAMPLES=OFF \
    -DBUILD_PYTHON_BINDINGS=OFF \
    -DBUILD_C_BINDINGS=ON
  cmake --build "$HOST_BUILD" --config Release --target protoc -j"$JOBS"
  HOST_PROTOC="$(find_host_protoc)" || {
    echo "Could not locate host protoc under $HOST_BUILD" >&2
    find "$HOST_BUILD" -name 'protoc*' 2>/dev/null | head -50 >&2 || true
    exit 1
  }
  echo "Host protoc: $HOST_PROTOC"
fi

cmake -S "$NATIVE" -B "$BUILD_DIR" \
  -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
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

# Locate shared lib (name may be libzvec_c_api.so)
LIB="$(find "$BUILD_DIR" -name 'libzvec_c_api.so' -o -name 'zvec_c_api.so' | head -n1)"
if [[ -z "$LIB" ]]; then
  echo "Could not find libzvec_c_api.so under $BUILD_DIR" >&2
  find "$BUILD_DIR" -name '*.so' | head -50 >&2 || true
  exit 1
fi

"$ROOT/build/ci/deploy-native.sh" "$RID" "$LIB"
