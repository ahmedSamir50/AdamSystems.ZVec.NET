#!/usr/bin/env bash
# Host entry (Unix / WSL): build linux-x64 natives via Docker.
# On Windows use: powershell -NoProfile -File build/ci/docker-linux-native.ps1
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
# Normalize inner script to LF
sed -i 's/\r$//' "${ROOT}/build/ci/docker-linux-native-inner.sh" || true
chmod +x "${ROOT}/build/ci/docker-linux-native-inner.sh"
rm -rf "${ROOT}/src/Native/ZVec.Native/build-linux-x64"

docker run --rm \
  -v "${ROOT}:/src" \
  -w /src \
  -e CMAKE_POLICY_VERSION_MINIMUM=3.5 \
  ubuntu:24.04 \
  bash /src/build/ci/docker-linux-native-inner.sh

ls -la "${ROOT}/_sim_natives/zvec-native-linux-x64"
git -C "${ROOT}/src/Native/ZVec.Native/external/zvec" reset --hard v0.7.0
git -C "${ROOT}/src/Native/ZVec.Native/external/zvec" clean -ffdx
git -C "${ROOT}/src/Native/ZVec.Native/external/zvec" submodule foreach --recursive 'git reset --hard; git clean -ffdx' || true
echo DOCKER_LINUX_NATIVE_DONE
