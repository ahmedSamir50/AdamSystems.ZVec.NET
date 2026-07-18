#!/usr/bin/env bash
# Create a clean console app, restore ZVec.NET from a local nupkg folder, smoke-open a collection.
set -euo pipefail

RID="${1:?rid}"
FEED_DIR="${2:?path to folder containing .nupkg}"
# Optional: pre-downloaded native artifact dir (Pack CI downloads to _rid_native).
NATIVE_SRC="${3:-}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

FEED_DIR="$(cd "$FEED_DIR" && pwd)"
NUPKG=$(ls "$FEED_DIR"/ZVec.NET.*.nupkg | head -n1)
test -f "$NUPKG"
VERSION=$(basename "$NUPKG" .nupkg | sed 's/^ZVec\.NET\.//')

echo "Validating package $NUPKG (version=$VERSION) for RID=$RID"
echo "ROOT=$ROOT WORK=$WORK"

mkdir -p "$WORK/feed"
cp "$NUPKG" "$WORK/feed/"
cp "$FEED_DIR"/ZVec.NET.*.snupkg "$WORK/feed/" 2>/dev/null || true

# Inspect nupkg runtime layout (diagnose RID asset packaging).
mkdir -p "$WORK/nupkg_ex"
python - "$NUPKG" "$WORK/nupkg_ex" <<'PY'
import zipfile, sys
nupkg, out = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(nupkg) as z:
    for n in z.namelist():
        if "runtimes/" in n or n.startswith("lib/"):
            print(n)
    z.extractall(out)
    print("extracted", len(z.namelist()), "entries")
PY
echo "=== extracted runtimes tree ==="
find "$WORK/nupkg_ex/runtimes" -type f 2>/dev/null | sort || echo "(no runtimes/ in nupkg)"

# Local feed for ZVec.NET + nuget.org for transitive Microsoft.Extensions.* deps.
cat > "$WORK/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$WORK/feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

dotnet new console -n ConsumerSmoke -o "$WORK/app" --framework net8.0 --force
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

cat > "$WORK/app/Program.cs" <<'EOF'
using ZVec.NET;

var path = Path.Combine(Path.GetTempPath(), "zvec-consumer-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(path);
Console.WriteLine("RID=" + System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier);
Console.WriteLine("BaseDir=" + AppContext.BaseDirectory);
try
{
    using var factory = new ZVecFactory();
    factory.Initialize(new ZVecOptions { LogLevel = ZVecLogLevel.Warn });
    var schema = new ZVecCollectionSchemaBuilder("smoke")
        .AddVector("embedding", ZVecDataType.VectorFp32, 8, new ZVecFlatIndexParam())
        .Build();
    using (var col = factory.CreateAndOpen(path, schema))
    {
        Console.WriteLine("OK: collection created at " + path);
    }
    factory.Shutdown();
    Environment.ExitCode = 0;
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
finally
{
    try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { /* ignore */ }
}
EOF

dotnet restore "$WORK/app/ConsumerSmoke.csproj" --runtime "$RID"
dotnet build "$WORK/app/ConsumerSmoke.csproj" -c Release --no-restore

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

dotnet run --project "$WORK/app/ConsumerSmoke.csproj" -c Release --no-build --runtime "$RID"
echo "Consumer smoke passed for $RID"