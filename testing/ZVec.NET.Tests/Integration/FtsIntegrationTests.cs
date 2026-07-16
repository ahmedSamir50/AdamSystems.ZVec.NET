using FluentAssertions;

namespace ZVec.NET.Tests.Integration;

public class FtsIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public FtsIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_fts_integration_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
        {
            Name = "fts_integration",
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
    }

    private void Setup()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        _collection = _factory.CreateAndOpen(_testPath, _schema);
    }

    [Fact]
    public void Test_Fts_Query_Recalls_Matches()
    {
        Setup();
        _collection.Should().NotBeNull();

        // Insert documents with text content
        var docs = new[]
        {
            ZVecDoc.Create("doc1", fields: new Dictionary<string, object> { ["content"] = "Alibaba ZVec is an in-process vector database." }),
            ZVecDoc.Create("doc2", fields: new Dictionary<string, object> { ["content"] = "SQLite is a lightweight SQL database engine." }),
            ZVecDoc.Create("doc3", fields: new Dictionary<string, object> { ["content"] = "Vector databases are useful for semantic search and HNSW retrieval." }),
        };

        var insertResult = _collection!.Insert(docs);
        insertResult.IsSuccess.Should().BeTrue();

        // Query FTS for "vector database"
        var query = new ZVecQuery
        {
            FieldName = "content",
            Fts = new ZVecFtsQuery { QueryString = "vector database", DefaultOperator = ZVecFtsDefaultOperator.And }
        };

        var results = _collection.Query(query, topk: 5);

        results.Should().NotBeEmpty();
        // Since doc1 and doc3 contain 'vector' and 'database', they should be returned
        results.Should().Contain(d => d.Id == "doc1" || d.Id == "doc3");
        results.Should().NotContain(d => d.Id == "doc2");
    }

    public void Dispose()
    {
        _collection?.Dispose();
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try
            {
                Directory.Delete(_testPath, true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }
}
