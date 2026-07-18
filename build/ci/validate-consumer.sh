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

mkdir -p "$WORK/feed"
cp "$NUPKG" "$WORK/feed/"
cp "$FEED_DIR"/ZVec.NET.*.snupkg "$WORK/feed/" 2>/dev/null || true

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
(
  cd "$WORK/app"
  # Prefer the temp nuget.config (local + nuget.org) for restore/add.
  cp "$WORK/nuget.config" .
  dotnet add ConsumerSmoke.csproj package ZVec.NET --version "$VERSION"
)

cat > "$WORK/app/Program.cs" <<'EOF'
using ZVec.NET;

var path = Path.Combine(Path.GetTempPath(), "zvec-consumer-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(path);
try
{
    using var factory = new ZVecFactory();
    factory.Initialize(new ZVecOptions { LogLevel = ZVecLogLevel.Warn });
    var schema = new ZVecCollectionSchemaBuilder("smoke")
        .AddVector("embedding", ZVecDataType.VectorFp32, 8, new ZVecFlatIndexParam())
        .Build();
    using var col = factory.CreateAndOpen(path, schema);
    Console.WriteLine("OK: collection created at " + path);
}
catch (DllNotFoundException ex)
{
    Console.Error.WriteLine("NATIVE_MISSING: " + ex.Message);
    Environment.Exit(2);
}
finally
{
    try { Directory.Delete(path, recursive: true); } catch { /* ignore */ }
}
EOF

dotnet build "$WORK/app/ConsumerSmoke.csproj" -c Release

# Deploy RID natives into the consumer output (from CI artifact and/or NuGet package assets).
OUT_NATIVE="$WORK/app/bin/Release/net8.0/runtimes/${RID}/native"
mkdir -p "$OUT_NATIVE"
if [ -n "$NATIVE_SRC" ] && [ -d "$NATIVE_SRC" ]; then
  shopt -s nullglob
  files=("$NATIVE_SRC"/*)
  if [ ${#files[@]} -gt 0 ]; then
    cp -f "$NATIVE_SRC"/* "$OUT_NATIVE/"
    echo "Deployed CI native artifact into $OUT_NATIVE"
    ls -la "$OUT_NATIVE"
  fi
fi

dotnet run --project "$WORK/app/ConsumerSmoke.csproj" -c Release --no-build
echo "Consumer smoke passed for $RID"
