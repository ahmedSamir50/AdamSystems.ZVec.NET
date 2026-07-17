using FluentAssertions;
using ZVec.NET.Query;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// Broad native-caller smoke: upsert, delete-with-results, optimize, drop-index,
/// and IVF/FTS index-param build paths that existing tests may not hit.
/// </summary>
public class NativeCallerCoverageIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public NativeCallerCoverageIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_caller_cov_{Guid.NewGuid():N}");
    }

    [Fact]
    public void Upsert_DeleteWithResults_Optimize_DropIndex_Succeed()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var schema = new ZVecCollectionSchema
        {
            Name = "caller_cov",
            Fields =
            [
                new ZVecFieldSchema { Name = "body", DataType = ZVecDataType.String }
            ],
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecIvfIndexParam { CentroidsNum = 4 }
                }
            ]
        };

        _collection = _factory.CreateAndOpen(_testPath, schema);

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("c1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["body"] = "hello" });

        _collection.Upsert(doc).IsSuccess.Should().BeTrue();
        _collection.Upsert(doc).IsSuccess.Should().BeTrue();

        var deleteResults = _collection.DeleteWithResults(["c1"]);
        deleteResults.Should().NotBeEmpty();

        _collection.Optimize();
        _collection.DropIndex("embedding");
        _collection.CreateIndex("embedding", new ZVecFlatIndexParam());
    }

    [Fact]
    public void Fts_Index_Param_And_Query_Path_Succeed()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var ftsPath = _testPath + "_fts";
        // FTS indexes attach to string vector fields (see FtsIntegrationTests).
        var schema = new ZVecCollectionSchema
        {
            Name = "caller_fts",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "content",
                    DataType = ZVecDataType.String,
                    Dimension = 0,
                    IndexParam = new ZVecFtsIndexParam
                    {
                        Tokenizer = ZVecFtsTokenizer.Standard,
                        Filters = [ZVecFtsTokenFilter.Lowercase]
                    }
                }
            ]
        };

        using var col = _factory.CreateAndOpen(ftsPath, schema);
        col.Insert(ZVecDoc.Create("f1",
            fields: new Dictionary<string, object> { ["content"] = "alpha beta gamma" }))
            .IsSuccess.Should().BeTrue();

        var results = col.Query(new ZVecQuery
        {
            FieldName = "content",
            Fts = new ZVecFtsQuery { QueryString = "alpha" }
        }, topk: 5);

        results.Should().NotBeNull();

        try
        {
            if (Directory.Exists(ftsPath))
                Directory.Delete(ftsPath, true);
        }
        catch { /* ignore */ }
    }

    public void Dispose()
    {
        _collection?.Dispose();
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try { Directory.Delete(_testPath, true); }
            catch { /* ignore */ }
        }
    }
}
