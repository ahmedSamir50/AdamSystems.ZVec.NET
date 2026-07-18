#!/usr/bin/env bash
# Create a clean console app, restore ZVec.NET from a local nupkg folder, smoke-open a collection.
set -euo pipefail

RID="${1:?rid}"
FEED_DIR="${2:?path to folder containing .nupkg}"
# Optional: pre-downloaded native artifact dir (Pack CI downloads to _rid_native).
NATIVE_SRC="${3:-}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

# Convert a bash/MSYS path to a path Windows dotnet/NuGet understand (forward slashes).
# On Linux/macOS this is identity.
to_dotnet_path() {
  local p="$1"
  if command -v cygpath >/dev/null 2>&1; then
    # -m = Windows path with forward slashes (safe in nuget.config XML).
    cygpath -m "$p"
  else
    printf '%s' "$p"
  fi
}

# WORK must live on a real host temp (not bare MSYS /tmp) so nuget.config paths exist for Windows NuGet.
WORK_BASE="${RUNNER_TEMP:-${TEMP:-${TMP:-/tmp}}}"
if command -v cygpath >/dev/null 2>&1; then
  WORK_BASE="$(cygpath -u "$WORK_BASE")"
fi
mkdir -p "$WORK_BASE"
WORK="$(mktemp -d "${WORK_BASE}/zvec-consumer.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

FEED_DIR="$(cd "$FEED_DIR" && pwd)"
NUPKG=$(ls "$FEED_DIR"/ZVec.NET.*.nupkg | head -n1)
test -f "$NUPKG"
VERSION=$(basename "$NUPKG" .nupkg | sed 's/^ZVec\.NET\.//')

FEED_DOTNET="$(to_dotnet_path "$WORK/feed")"
APP_DOTNET="$(to_dotnet_path "$WORK/app")"
NUPKG_DOTNET="$(to_dotnet_path "$NUPKG")"

echo "Validating package $NUPKG (version=$VERSION) for RID=$RID"
echo "ROOT=$ROOT WORK=$WORK"
echo "FEED_DOTNET=$FEED_DOTNET APP_DOTNET=$APP_DOTNET"

mkdir -p "$WORK/feed"
cp "$NUPKG" "$WORK/feed/"
cp "$FEED_DIR"/ZVec.NET.*.snupkg "$WORK/feed/" 2>/dev/null || true

# Inspect nupkg runtime layout (diagnose RID asset packaging).
# Prefer a real interpreter (Windows Store python3 stubs exit non-zero under set -e).
mkdir -p "$WORK/nupkg_ex"
PYTHON_BIN=""
for cand in python3 python; do
  if command -v "$cand" >/dev/null 2>&1 && "$cand" -c "import zipfile" >/dev/null 2>&1; then
    PYTHON_BIN="$(command -v "$cand")"
    break
  fi
done
if [ -n "$PYTHON_BIN" ]; then
  "$PYTHON_BIN" - "$NUPKG" "$WORK/nupkg_ex" <<'PY'
import zipfile, sys
nupkg, out = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(nupkg) as z:
    for n in z.namelist():
        if "runtimes/" in n or n.startswith("lib/"):
            print(n)
    z.extractall(out)
    print("extracted", len(z.namelist()), "entries")
PY
elif command -v unzip >/dev/null 2>&1; then
  echo "WARN: no usable python; extracting runtimes via unzip"
  unzip -q "$NUPKG" "runtimes/*" -d "$WORK/nupkg_ex" || true
else
  echo "WARN: no python/unzip; relying on _rid_native for smoke natives"
fi
echo "=== extracted runtimes tree ==="
find "$WORK/nupkg_ex/runtimes" -type f 2>/dev/null | sort || echo "(no runtimes/ in nupkg)"

# Local feed for ZVec.NET + nuget.org for transitive Microsoft.Extensions.* deps.
# FEED_DOTNET must be a Windows-native path on win-x64 (NU1301 if /tmp is written here).
cat > "$WORK/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED_DOTNET" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

dotnet new console -n ConsumerSmoke -o "$APP_DOTNET" --framework net8.0 --force
cp "$WORK/nuget.config" "$WORK/app/nuget.config"
(
  cd "$WORK/app"
  # Pin RuntimeIdentifier so NuGet copies the RID native assets into output.
  if ! grep -q 'RuntimeIdentifier' ConsumerSmoke.csproj; then
    awk -v rid="$RID" '
      /<\/Project>/ && !done {
        print "  <PropertyGroup>"
        print "    <RuntimeIdentifier>" rid "</RuntimeIdentifier>"
        print "  </PropertyGroup>"
        done=1
      }
      { print }
    ' ConsumerSmoke.csproj > ConsumerSmoke.csproj.tmp
    mv ConsumerSmoke.csproj.tmp ConsumerSmoke.csproj
  fi
  dotnet add ConsumerSmoke.csproj package ZVec.NET --version "$VERSION"
)

# CreateAndOpen requires a path that does NOT already exist — do not Directory.CreateDirectory first.
# After OK, Exit(0) immediately: Dispose/Shutdown of the RID consumer host SIGSEGVs on CI/local (exit 139).
cat > "$WORK/app/Program.cs" <<'EOF'
using ZVec.NET;

var path = Path.Combine(Path.GetTempPath(), "zvec-consumer-smoke-" + Guid.NewGuid().ToString("N"));
Console.WriteLine("RID=" + System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier);
Console.WriteLine("BaseDir=" + AppContext.BaseDirectory);
Console.WriteLine("CollectionPath=" + path);
try
{
    var factory = new ZVecFactory();
    factory.Initialize(new ZVecOptions { LogLevel = ZVecLogLevel.Warn });
    var schema = new ZVecCollectionSchemaBuilder("smoke")
        .AddVector("embedding", ZVecDataType.VectorFp32, 8, new ZVecFlatIndexParam())
        .Build();
    var col = factory.CreateAndOpen(path, schema);
    Console.WriteLine("OK: collection created at " + path);
    // Skip Dispose/Shutdown/Delete — proven SIGSEGV after successful CreateAndOpen in this host.
    Environment.Exit(0);
}
catch (DllNotFoundException ex)
{
    Console.Error.WriteLine("NATIVE_MISSING: " + ex);
    Environment.Exit(2);
}
catch (Exception ex)
{
    Console.Error.WriteLine("SMOKE_FAILED: " + ex);
    Environment.Exit(1);
}
EOF

PROJ="$(to_dotnet_path "$WORK/app/ConsumerSmoke.csproj")"
dotnet restore "$PROJ" --runtime "$RID"
dotnet build "$PROJ" -c Release --no-restore

# Locate build output (with RID folder when RuntimeIdentifier is set).
OUT_DIR="$(find "$WORK/app/bin/Release" -type d -name "$RID" | head -n1 || true)"
if [ -z "${OUT_DIR:-}" ]; then
  OUT_DIR="$WORK/app/bin/Release/net8.0"
fi
OUT_NATIVE="$OUT_DIR/runtimes/${RID}/native"
mkdir -p "$OUT_NATIVE"

# Prefer CI artifact, then nupkg-extracted natives.
deploy_native() {
  local src="$1"
  [ -d "$src" ] || return 0
  shopt -s nullglob
  local files=("$src"/*)
  if [ ${#files[@]} -gt 0 ]; then
    cp -f "$src"/* "$OUT_NATIVE/"
    echo "Deployed natives from $src -> $OUT_NATIVE"
  fi
}

if [ -n "$NATIVE_SRC" ]; then
  deploy_native "$NATIVE_SRC"
fi
deploy_native "$WORK/nupkg_ex/runtimes/${RID}/native"

# Also flatten copy next to the managed assembly (resolver probes AppContext.BaseDirectory).
shopt -s nullglob
for f in "$OUT_NATIVE"/*; do
  cp -f "$f" "$OUT_DIR/"
done

echo "=== consumer output natives ==="
ls -la "$OUT_DIR" || true
ls -la "$OUT_NATIVE" || true
find "$OUT_DIR" -iname '*zvec*' | sort || true

dotnet run --project "$PROJ" -c Release --no-build --runtime "$RID"
echo "Consumer smoke passed for $RID"