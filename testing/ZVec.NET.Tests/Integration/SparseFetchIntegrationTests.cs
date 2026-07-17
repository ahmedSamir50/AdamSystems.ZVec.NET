using FluentAssertions;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// Exercises sparse insert + fetch unmarshall via zvec_doc_get_field_value_copy packing.
/// Would have caught the orphan zvec_doc_get_sparse_vector_field.
/// </summary>
public class SparseFetchIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public SparseFetchIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_sparse_fetch_{Guid.NewGuid():N}");
    }

    [Fact]
    public void Insert_Sparse_Fetch_RoundTrips_Indices_And_Values()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var schema = new ZVecCollectionSchema
        {
            Name = "sparse_fetch",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "dense",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecFlatIndexParam()
                },
                new ZVecVectorSchema
                {
                    Name = "sparse",
                    DataType = ZVecDataType.SparseVectorFp32,
                    Dimension = 64,
                    IndexParam = new ZVecFlatIndexParam { MetricType = ZVecMetricType.Ip }
                }
            ]
        };

        _collection = _factory.CreateAndOpen(_testPath, schema);

        var sparse = new Dictionary<int, float> { [2] = 1.5f, [7] = -2.5f, [15] = 3.5f };
        var doc = ZVecDoc.Create("s1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
            {
                ["dense"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
            },
            sparseVectors: new Dictionary<string, IReadOnlyDictionary<int, float>>
            {
                ["sparse"] = sparse
            });

        _collection.Insert(doc).IsSuccess.Should().BeTrue();

        var fetched = _collection.Fetch("s1", includeVector: true);
        fetched.Should().NotBeNull();
        fetched!.SparseVectors.Should().ContainKey("sparse");
        fetched.SparseVectors["sparse"].Should().BeEquivalentTo(sparse);
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
