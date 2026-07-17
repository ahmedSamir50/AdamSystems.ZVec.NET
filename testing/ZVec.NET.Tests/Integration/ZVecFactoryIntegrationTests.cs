using FluentAssertions;
using ZVec.NET.Exceptions;

namespace ZVec.NET.Tests.Integration;

/// <summary>US-E10 — factory initialize/shutdown, ABI gate, and collection open paths.</summary>
[Collection(nameof(NativeSessionCollection))]
public class ZVecFactoryIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private ZVecFactory? _factory;

    public ZVecFactoryIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_factory_{Guid.NewGuid():N}");
        ResetVersionGateDefaults();
    }

    private static void ResetVersionGateDefaults()
    {
        ZVecDefaults.Version.BypassAbiCheck = false;
        ZVecDefaults.Version.ExpectedMajor = ZVecNativeAbi.MinimumMajor;
        ZVecDefaults.Version.ExpectedMinor = ZVecNativeAbi.MinimumMinor;
        ZVecDefaults.Version.ExpectedPatch = ZVecNativeAbi.MinimumPatch;
    }

    [Fact]
    public void Initialize_WhenNativeAvailable_Succeeds()
    {
        _fixture.SkipIfNotAvailable();

        _factory = new ZVecFactory();
        _factory.Initialize();
        _factory.IsInitialized.Should().BeTrue();
        _factory.GetNativeVersion().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Initialize_SecondCall_IsNoOp()
    {
        _fixture.SkipIfNotAvailable();

        _factory = new ZVecFactory();
        _factory.Initialize();
        _factory.Initialize();
        _factory.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void Initialize_WithAbiMismatch_Throws()
    {
        _fixture.SkipIfNotAvailable();

        var previousMajor = ZVecDefaults.Version.ExpectedMajor;
        var previousMinor = ZVecDefaults.Version.ExpectedMinor;
        var previousPatch = ZVecDefaults.Version.ExpectedPatch;
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;

        try
        {
            ZVecDefaults.Version.BypassAbiCheck = false;
            ZVecDefaults.Version.ExpectedMajor = ZVecNativeAbi.MinimumMajor + 99;
            ZVecDefaults.Version.ExpectedMinor = 0;
            ZVecDefaults.Version.ExpectedPatch = 0;

            _factory = new ZVecFactory();
            var act = () => _factory.Initialize();
            act.Should().Throw<ZVecAbiMismatchException>();
        }
        finally
        {
            ZVecDefaults.Version.ExpectedMajor = previousMajor;
            ZVecDefaults.Version.ExpectedMinor = previousMinor;
            ZVecDefaults.Version.ExpectedPatch = previousPatch;
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    [Fact]
    public void Dispose_AfterInitialize_ShutsDown()
    {
        _fixture.SkipIfNotAvailable();

        _factory = new ZVecFactory();
        _factory.Initialize();
        _factory.IsInitialized.Should().BeTrue();

        _factory.Dispose();
        _factory.IsInitialized.Should().BeFalse();
        _factory = null;
    }

    [Fact]
    public void GetAbiInfo_WhenInitialized_ReportsCompatibleNative()
    {
        _fixture.SkipIfNotAvailable();

        var previousMajor = ZVecDefaults.Version.ExpectedMajor;
        var previousMinor = ZVecDefaults.Version.ExpectedMinor;
        var previousPatch = ZVecDefaults.Version.ExpectedPatch;
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;

        try
        {
            ZVecDefaults.Version.BypassAbiCheck = false;
            ZVecDefaults.Version.ExpectedMajor = ZVecNativeAbi.MinimumMajor;
            ZVecDefaults.Version.ExpectedMinor = ZVecNativeAbi.MinimumMinor;
            ZVecDefaults.Version.ExpectedPatch = ZVecNativeAbi.MinimumPatch;

            _factory = new ZVecFactory();
            _factory.Initialize();

            var abi = _factory.GetAbiInfo();
            abi.FoundVersion.Should().NotBeNullOrWhiteSpace();
            abi.FoundMajor.Should().Be(ZVecNativeAbi.MinimumMajor);
            abi.IsCompatible.Should().BeTrue();
        }
        finally
        {
            ZVecDefaults.Version.ExpectedMajor = previousMajor;
            ZVecDefaults.Version.ExpectedMinor = previousMinor;
            ZVecDefaults.Version.ExpectedPatch = previousPatch;
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    [Fact]
    public void CreateAndOpen_WithPathAndSchema_Succeeds()
    {
        _fixture.SkipIfNotAvailable();

        _factory = new ZVecFactory();
        _factory.Initialize();

        var schema = new ZVecCollectionSchema
        {
            Name = "factory_open",
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

        using var col = _factory.CreateAndOpen(_testPath, schema);
        col.Should().NotBeNull();
        (Directory.Exists(_testPath) || File.Exists(_testPath)).Should().BeTrue();
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
