#!/usr/bin/env bash
# Create a clean console app, restore ZVec.NET from a local nupkg folder, smoke-open a collection.
set -euo pipefail

RID="${1:?rid}"
FEED_DIR="${2:?path to folder containing .nupkg}"
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
# snupkg optional
cp "$FEED_DIR"/ZVec.NET.*.snupkg "$WORK/feed/" 2>/dev/null || true

cat > "$WORK/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$WORK/feed" />
  </packageSources>
</configuration>
EOF

dotnet new console -n ConsumerSmoke -o "$WORK/app" --framework net8.0 --force
dotnet add "$WORK/app/ConsumerSmoke.csproj" package ZVec.NET --version "$VERSION" --source "$WORK/feed"

cat > "$WORK/app/Program.cs" <<'EOF'
using ZVec.NET;

var path = Path.Combine(Path.GetTempPath(), "zvec-consumer-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(path);
try
{
    using var factory = new ZVecFactory();
    factory.Initialize(new ZVecOptions { LogLevel = ZVecLogLevel.Warn });
    var schema = new ZVecCollectionSchemaBuilder("smoke")
        .AddVector("embedding", ZVecDataType.Float, 8)
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
dotnet run --project "$WORK/app/ConsumerSmoke.csproj" -c Release --no-build
echo "Consumer smoke passed for $RID"
