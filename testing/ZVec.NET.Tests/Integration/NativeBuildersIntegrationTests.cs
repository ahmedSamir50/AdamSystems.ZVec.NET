using FluentAssertions;
using ZVec.NET.Internal;
using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// Real-native coverage for Native*Builder and NativeQueryParamsApplicator flow paths
/// (all query-param types × Vector / Sub / GroupBy targets; index-param types including EnableRotate).
/// </summary>
[Collection(nameof(NativeSessionCollection))]
public class NativeBuildersIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _root;
    private readonly List<string> _paths = [];
    private IZvecFactory? _factory;

    public NativeBuildersIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _root = Path.Combine(Path.GetTempPath(), $"zvec_builders_{Guid.NewGuid():N}");
    }

    private IZvecFactory EnsureFactory()
    {
        _fixture.SkipIfNotAvailable();
        _factory ??= new ZVecFactory();
        _factory.Initialize();
        return _factory;
    }

    public void Dispose()
    {
        _factory?.Shutdown();
        foreach (var p in _paths.Append(_root))
        {
            try
            {
                if (Directory.Exists(p))
                    Directory.Delete(p, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    private static float[] Vec4() => [0.1f, 0.2f, 0.3f, 0.4f];

    [Theory]
    [MemberData(nameof(AllQueryParamsCases))]
    public void QueryParamsApplicator_Apply_On_Vector_Sub_And_GroupBy_Targets(ZVecQueryParams queryParams)
    {
        EnsureFactory();

        nint vectorQuery = NativeMethods.zvec_vector_query_create();
        nint subQuery = NativeMethods.zvec_sub_query_create();
        nint groupQuery = NativeMethods.zvec_group_by_vector_query_create();
        try
        {
            vectorQuery.Should().NotBe(IntPtr.Zero);
            subQuery.Should().NotBe(IntPtr.Zero);
            groupQuery.Should().NotBe(IntPtr.Zero);

            var actVector = () => NativeQueryParamsApplicator.Apply(
                vectorQuery, queryParams, NativeQueryParamsApplicator.QueryTarget.VectorQuery);
            var actSub = () => NativeQueryParamsApplicator.Apply(
                subQuery, queryParams, NativeQueryParamsApplicator.QueryTarget.SubQuery);
            var actGroup = () => NativeQueryParamsApplicator.Apply(
                groupQuery, queryParams, NativeQueryParamsApplicator.QueryTarget.GroupByQuery);

            actVector.Should().NotThrow();
            actSub.Should().NotThrow();
            actGroup.Should().NotThrow();
        }
        finally
        {
            if (vectorQuery != IntPtr.Zero) NativeMethods.zvec_vector_query_destroy(vectorQuery);
            if (subQuery != IntPtr.Zero) NativeMethods.zvec_sub_query_destroy(subQuery);
            if (groupQuery != IntPtr.Zero) NativeMethods.zvec_group_by_vector_query_destroy(groupQuery);
        }
    }

    public static TheoryData<ZVecQueryParams> AllQueryParamsCases() =>
    [
        new ZVecHnswQueryParams
        {
            EfSearch = 64,
            Radius = 0f,
            IsLinear = false,
            IsUsingRefiner = false
        },
        new ZVecIvfQueryParams
        {
            Nprobe = 8,
            Radius = 0f,
            IsLinear = false,
            IsUsingRefiner = false,
            ScaleFactor = 10f
        },
        new ZVecFlatQueryParams
        {
            Radius = 0f,
            IsLinear = true,
            IsUsingRefiner = false,
            ScaleFactor = 10f
        },
        new ZVecVamanaQueryParams
        {
            EfSearch = 32,
            Radius = 0f,
            IsLinear = false,
            IsUsingRefiner = false
        },
        new ZVecDiskAnnQueryParams
        {
            ListSize = 64,
            Radius = 0f,
            IsLinear = false,
            IsUsingRefiner = false
        }
    ];

    [Fact]
    public void NativeQueryBuilder_With_Each_QueryParams_Type_Succeeds()
    {
        EnsureFactory();
        foreach (ZVecQueryParams qp in AllQueryParamsCases())
        {
            var query = new ZVecQuery
            {
                FieldName = "embedding",
                Vector = Vec4(),
                QueryParams = qp
            };
            using var builder = new NativeQueryBuilder(query, topk: 5, filter: null, includeVector: false);
            builder.Handle.Should().NotBe(IntPtr.Zero);
        }
    }

    [Fact]
    public void NativeQueryBuilder_With_Filter_And_Fts_Succeeds()
    {
        EnsureFactory();
        var query = new ZVecQuery
        {
            FieldName = "content",
            Fts = new ZVecFtsQuery { QueryString = "hello", DefaultOperator = ZVecFtsDefaultOperator.And }
        };
        using var builder = new NativeQueryBuilder(query, topk: 3, filter: "id != \"\"", includeVector: false);
        builder.Handle.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void NativeMultiQueryBuilder_Applies_PerSub_QueryParams()
    {
        EnsureFactory();
        var queries = new List<ZVecQuery>
        {
            new()
            {
                FieldName = "embedding",
                Vector = Vec4(),
                QueryParams = new ZVecHnswQueryParams { EfSearch = 32, IsLinear = false }
            },
            new()
            {
                FieldName = "embedding",
                Vector = Vec4(),
                QueryParams = new ZVecFlatQueryParams { IsLinear = true, ScaleFactor = 5f }
            }
        };

        using var builder = new NativeMultiQueryBuilder(queries, topk: 5, reranker: null, filter: null);
        builder.Handle.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void NativeGroupByQueryBuilder_With_Filter_And_Each_QueryParams_Succeeds()
    {
        EnsureFactory();
        foreach (ZVecQueryParams qp in AllQueryParamsCases())
        {
            var gq = new ZVecGroupByQuery
            {
                GroupByField = "category",
                GroupSize = 2,
                Topk = 3,
                Filter = "category != \"\"",
                Query = new ZVecQuery
                {
                    FieldName = "embedding",
                    Vector = Vec4(),
                    QueryParams = qp
                }
            };
            using var builder = new NativeGroupByQueryBuilder(gq, includeVector: false);
            builder.Handle.Should().NotBe(IntPtr.Zero);
        }
    }

    [Theory]
    [MemberData(nameof(IndexParamCases))]
    public void NativeIndexParamBuilder_Each_IndexType_Succeeds(ZVecIndexParam param)
    {
        EnsureFactory();
        if (param is ZVecDiskAnnIndexParam && !OperatingSystem.IsLinux())
        {
            // DiskANN create is Linux-gated in platform requirements.
            var act = () => new NativeIndexParamBuilder(param);
            act.Should().Throw<PlatformNotSupportedException>();
            return;
        }

        using var builder = new NativeIndexParamBuilder(param);
        builder.Handle.Should().NotBe(IntPtr.Zero);
    }

    public static TheoryData<ZVecIndexParam> IndexParamCases() =>
    [
        new ZVecFlatIndexParam(),
        new ZVecFlatIndexParam { QuantizeType = ZVecQuantizeType.Int8, EnableRotate = true },
        new ZVecHnswIndexParam { M = 8, EfConstruction = 64, QuantizeType = ZVecQuantizeType.Int8, EnableRotate = true },
        // HnswRabitq excluded: NativeIndexParamBuilder throws (ARM platform gate or C API gap).
        // See ZVecPlatformRequirementsTests — not covered by e2e index create.
        new ZVecIvfIndexParam { CentroidsNum = 4, QuantizeType = ZVecQuantizeType.Int4, EnableRotate = true },
        new ZVecVamanaIndexParam { MaxDegree = 16, SearchListSize = 32, EnableRotate = false },
        new ZVecDiskAnnIndexParam { MaxDegree = 16, ListSize = 32, PqChunkNum = 4 },
        new ZVecInvertIndexParam(),
        new ZVecFtsIndexParam
        {
            Tokenizer = ZVecFtsTokenizer.Standard,
            Filters = [ZVecFtsTokenFilter.AsciiFolding, ZVecFtsTokenFilter.Stemmer]
        }
    ];

    [Fact]
    public void EndToEnd_MultiQuery_With_PerSub_HnswParams_ReturnsHits()
    {
        var factory = EnsureFactory();
        var path = _root + "_multi";
        _paths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "multi_qp",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecHnswIndexParam { M = 8, EfConstruction = 64 }
                }
            ]
        };

        using var col = factory.CreateAndOpen(path, schema);
        float[] vec = Vec4();
        col.Insert(ZVecDoc.Create("m1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            [
                new ZVecQuery
                {
                    FieldName = "embedding",
                    Vector = vec,
                    QueryParams = new ZVecHnswQueryParams { EfSearch = 64 }
                },
                new ZVecQuery
                {
                    FieldName = "embedding",
                    Vector = vec,
                    QueryParams = new ZVecHnswQueryParams { EfSearch = 32, IsLinear = false }
                }
            ],
            topk: 1,
            includeVector: false);
        hits.Should().ContainSingle().Which.Id.Should().Be("m1");
    }

    [Fact]
    public void EndToEnd_IvfQueryParams_On_Query_Succeeds()
    {
        var factory = EnsureFactory();
        var path = _root + "_ivf";
        _paths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "ivf_qp",
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

        using var col = factory.CreateAndOpen(path, schema);
        float[] vec = Vec4();
        col.Insert(ZVecDoc.Create("i1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            new ZVecQuery
            {
                FieldName = "embedding",
                Vector = vec,
                QueryParams = new ZVecIvfQueryParams
                {
                    Nprobe = 4,
                    ScaleFactor = 10f,
                    IsLinear = false
                }
            },
            topk: 1,
            includeVector: false);
        hits.Should().ContainSingle().Which.Id.Should().Be("i1");
    }
}
