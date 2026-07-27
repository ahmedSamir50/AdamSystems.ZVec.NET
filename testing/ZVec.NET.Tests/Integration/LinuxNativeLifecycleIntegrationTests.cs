using FluentAssertions;
using ZVec.NET.Exceptions;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// Regression tests for native init/teardown after fixing log-config ownership (alibaba/zvec#619).
/// </summary>
[Collection(nameof(NativeSessionCollection))]
public sealed class LinuxNativeLifecycleIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;

    public LinuxNativeLifecycleIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_linux_lifecycle_{Guid.NewGuid():N}");
    }

    [Fact]
    [Trait("Category", "LinuxTeardown")]
    public void Initialize_WithCustomLogConfig_DisposeShutdown_Reinitialize_ReopenSucceeds()
    {
        _fixture.SkipIfNotAvailable();

        var schema = new ZVecCollectionSchema
        {
            Name = "log_cfg_lifecycle",
            Fields =
            [
                new ZVecFieldSchema { Name = "title", DataType = ZVecDataType.String }
            ],
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecFlatIndexParam()
                }
            ]
        };

        _factory = new ZVecFactory();
        _factory.Initialize(new ZVecOptions
        {
            LogLevel = ZVecLogLevel.Warn,
            NativeTeardownPolicy = ZVecNativeTeardownPolicy.AlwaysCall
        });

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("persist1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["title"] = "lifecycle" });

        using (var col = _factory.CreateAndOpen(_testPath, schema))
        {
            col.Insert(doc).IsSuccess.Should().BeTrue();
        }

        _factory.Shutdown();
        _factory.IsInitialized.Should().BeFalse();

        _factory.Initialize(new ZVecOptions
        {
            LogLevel = ZVecLogLevel.Warn,
            NativeTeardownPolicy = ZVecNativeTeardownPolicy.AlwaysCall
        });

        using var reopened = _factory.Open(_testPath);
        var fetched = reopened.Fetch("persist1", includeVector: false);
        fetched.Should().NotBeNull();
        fetched!.Fields["title"].Should().Be("lifecycle");
    }

    public void Dispose()
    {
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try { Directory.Delete(_testPath, true); }
            catch { /* ignore cleanup */ }
        }
    }
}
