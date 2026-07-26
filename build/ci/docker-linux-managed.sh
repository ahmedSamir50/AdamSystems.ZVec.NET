#!/usr/bin/env bash
# Pack-parity Linux managed tests inside sdk:10.0-noble (GLIBC_2.38+).
# Expects repo mounted at /src (read-only OK). Copies a clean tree to /tmp.
set -euo pipefail

echo "Installing SDK 8 + 9 host packs into image DOTNET_ROOT..."
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet
bash /tmp/dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet
ls /usr/share/dotnet/packs/Microsoft.NETCore.App.Host.linux-x64

WORK=/tmp/zvec_managed_$$
mkdir -p "$WORK/src/Core" "$WORK/testing" "$WORK/build"
cp -a /src/src/Core/ZVec.NET "$WORK/src/Core/"
cp -a /src/testing/ZVec.NET.Tests "$WORK/testing/"
cp -a /src/build/ZVec.NET.snk "$WORK/build/"
cp -a /src/Directory.Build.props /src/Directory.Packages.props "$WORK/"
rm -rf "$WORK/src/Core/ZVec.NET/bin" "$WORK/src/Core/ZVec.NET/obj"
rm -rf "$WORK/testing/ZVec.NET.Tests/bin" "$WORK/testing/ZVec.NET.Tests/obj"
test -f "$WORK/src/Core/ZVec.NET/runtimes/linux-x64/native/libzvec_c_api.so"

cd "$WORK"
export NUGET_PACKAGES=/tmp/nuget_packages
export ZVEC_REQUIRE_NATIVE=1
mkdir -p "$NUGET_PACKAGES"
SNK="$WORK/build/ZVec.NET.snk"
echo "native OK; work=$WORK"

dotnet restore testing/ZVec.NET.Tests/ZVec.NET.Tests.csproj \
  -p:DisableImplicitNuGetFallbackFolder=true \
  -p:AssemblyOriginatorKeyFile="$SNK"

for tfm in net8.0 net9.0; do
  echo "=== $tfm ==="
  dotnet test testing/ZVec.NET.Tests/ZVec.NET.Tests.csproj -c Release -f "$tfm" \
    -p:DisableImplicitNuGetFallbackFolder=true \
    -p:AssemblyOriginatorKeyFile="$SNK" \
    --verbosity minimal
done

echo DOCKER_MANAGED_OK
