using FluentAssertions;

namespace ZVec.NET.Tests.Integration;

/// <summary>US-E18.8 — Destroy → directory gone.</summary>
public class DestroyIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private IZvecFactory? _factory;

    public DestroyIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_destroy_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
        {
            Name = "destroy_integration",
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
    }

    [Fact]
    public void Destroy_RemovesCollectionDirectory()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var col = _factory.CreateAndOpen(_testPath, _schema);
        Directory.Exists(_testPath).Should().BeTrue();

        col.Destroy();

        Directory.Exists(_testPath).Should().BeFalse();
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
