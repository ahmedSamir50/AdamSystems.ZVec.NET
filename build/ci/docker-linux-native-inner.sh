#!/usr/bin/env bash
# Inner script: runs inside ubuntu:24.04 with repo mounted at /src.
# GHA-aligned linux-x64 native build + Windows-mount CRLF fix (core.autocrlf).
#
# Build tree is under /tmp (container FS). Source stays on the mount so nested
# git submodules keep working (zvec cmake applies thirdparty patches via git).
# Do NOT tar-copy the tree — relative gitdir pointers break and arrow_fix fails.
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
export CMAKE_POLICY_VERSION_MINIMUM="${CMAKE_POLICY_VERSION_MINIMUM:-3.5}"

heartbeat() {
  while true; do
    sleep 60
    echo "HEARTBEAT $(date -u +%Y-%m-%dT%H:%M:%SZ) load=$(cut -d' ' -f1-3 /proc/loadavg) cc=$(pgrep -c cc1plus 2>/dev/null || echo 0)"
  done
}
heartbeat &
HB_PID=$!
trap 'kill "$HB_PID" 2>/dev/null || true' EXIT

apt-get update -qq
# GHA build-native.yml Linux tools + perl (snowball codegen shebangs)
apt-get install -y -qq ninja-build cmake build-essential git ca-certificates perl

ZVEC=/src/src/Native/ZVec.Native/external/zvec
NATIVE=/src/src/Native/ZVec.Native
BUILD=/tmp/zvec-build-linux-x64

# Windows bind-mounts often have CRLF text (core.autocrlf=true). GHA checkouts are LF.
echo "Normalizing LF under external/zvec (Windows mount CRLF guard)..."
find "$ZVEC" \( \
    -name '*.pl' -o -name '*.sh' -o -name '*.py' -o \
    -name 'GNUmakefile' -o -name 'Makefile' -o -name 'makefile' -o -name 'Makefile.*' \
  \) -type f -print0 2>/dev/null | xargs -0 -r sed -i 's/\r$//'
echo "LF_STRIP_DONE"

apply_one() {
  local repo="$1" patch="$2"
  if git -C "$repo" apply --check "$patch" 2>/dev/null; then
    git -C "$repo" apply "$patch"
    echo "APPLIED $patch"
  else
    echo "SKIP $patch (already applied or N/A)"
  fi
}
rm -rf "$BUILD"
echo "CMAKE_CONFIGURE_START $(date -u +%H:%M:%S) build=$BUILD"
cmake -B "$BUILD" -G Ninja -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
  -DOVERRIDE_GIT_DESCRIBE=v0.7.0 \
  -DBUILD_TESTING=OFF -DBUILD_TOOLS=OFF -DBUILD_EXAMPLES=OFF \
  -DBUILD_PYTHON_BINDINGS=OFF -DBUILD_C_BINDINGS=ON \
  -S "$NATIVE"
echo "CMAKE_BUILD_START $(date -u +%H:%M:%S) parallel=$(nproc)"
cmake --build "$BUILD" --target zvec_c_api --parallel "$(nproc)"
echo "CMAKE_BUILD_DONE $(date -u +%H:%M:%S)"

DLL="$(find "$BUILD" -name 'libzvec_c_api.so' | head -n1)"
test -n "$DLL" && test -f "$DLL"
ls -la "$DLL"
mkdir -p /src/_sim_natives/zvec-native-linux-x64
cp -f "$DLL" /src/_sim_natives/zvec-native-linux-x64/libzvec_c_api.so
cp -f "$DLL" /src/_sim_natives/zvec-native-linux-x64/zvec_c_api.so
ls -la /src/_sim_natives/zvec-native-linux-x64/
echo LINUX_NATIVE_OK
