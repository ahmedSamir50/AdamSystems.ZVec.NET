using System.Runtime.InteropServices;
using FluentAssertions;
using ZVec.NET.Internal;
using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Integration;

[Collection(nameof(NativeSessionCollection))]
public class V070ApiIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly List<string> _extraPaths = [];
    private IZvecFactory? _factory;

    public V070ApiIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_v070_{Guid.NewGuid():N}");
    }

    private IZvecFactory EnsureFactory()
    {
        _fixture.SkipIfNotAvailable();
        _factory ??= new ZVecFactory();
        _factory.Initialize();
        return _factory;
    }

    private static float[] RabitqVector(int seed = 1)
    {
        var v = new float[64];
        for (int i = 0; i < v.Length; i++)
            v[i] = (seed + i) * 0.01f;
        return v;
    }

    [Fact]
    public void Native_Version_Minor_Is_7()
    {
        EnsureFactory();
        NativeMethods.zvec_get_version_minor().Should().Be(7);
    }

    [Fact]
    public void IvfRabitq_CreateInsertQuery_Succeeds()
    {
        if (!OperatingSystem.IsLinux() ||
            RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
        {
            Assert.Skip("Upstream zvec 0.7.0 builds RaBitQ (IVF-RaBitQ) on Linux x86_64 only.");
        }

        var factory = EnsureFactory();
        var path = _testPath + "_ivfrq";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "ivf_rabitq",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 64,
                    IndexParam = new ZVecIvfRabitqIndexParam
                    {
                        Nlist = 4,
                        TotalBits = 7,
                        SampleCount = 0
                    }
                }
            ]
        };

        using var col = factory.CreateAndOpen(path, schema);
        var vec = RabitqVector();
        col.Insert(ZVecDoc.Create("rq1",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            new ZVecQuery
            {
                FieldName = "embedding",
                Vector = vec,
                QueryParams = new ZVecIvfRabitqQueryParams { Nprobe = 4 }
            },
            topk: 1,
            includeVector: false);
        hits.Should().ContainSingle().Which.Id.Should().Be("rq1");
    }

    [Fact]
    public void Fts_NgramTokenizer_Recalls()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_ngram";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "fts_ngram",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "content",
                    DataType = ZVecDataType.String,
                    Dimension = 0,
                    IndexParam = new ZVecFtsIndexParam
                    {
                        Tokenizer = ZVecFtsTokenizer.Ngram,
                        Filters = [ZVecFtsTokenFilter.Lowercase],
                        ExtraParams = new ZVecFtsExtraParams
                        {
                            NgramMin = 2,
                            NgramMax = 2,
                            TokenChars = ["letter"]
                        }
                    }
                }
            ]
        };

        using var col = factory.CreateAndOpen(path, schema);
        col.Insert(ZVecDoc.Create("ng1",
                fields: new Dictionary<string, object> { ["content"] = "database vectors" }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            new ZVecQuery
            {
                FieldName = "content",
                Fts = new ZVecFtsQuery
                {
                    QueryString = "database",
                    DefaultOperator = ZVecFtsDefaultOperator.And
                }
            },
            topk: 5);
        hits.Should().Contain(d => d.Id == "ng1");
    }

    [Fact]
    public void Vamana_TwoPassBuild_CreateInsertQuery_Succeeds()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_vamana2p";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "vamana_two_pass",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecVamanaIndexParam
                    {
                        MaxDegree = 8,
                        SearchListSize = 16,
                        TwoPassBuild = true
                    }
                }
            ]
        };

        using var col = factory.CreateAndOpen(path, schema);
        float[] vec = [0.1f, 0.2f, 0.3f, 0.4f];
        col.Insert(ZVecDoc.Create("v2p1",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            new ZVecQuery
            {
                FieldName = "embedding",
                Vector = vec,
                QueryParams = new ZVecVamanaQueryParams { EfSearch = 16 }
            },
            topk: 1,
            includeVector: false);
        hits.Should().ContainSingle().Which.Id.Should().Be("v2p1");
    }

    [Fact]
    public void Iterate_IsSnapshot_BeforeClose()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_iter";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "iter_snapshot",
            Fields = [new ZVecFieldSchema { Name = "title", DataType = ZVecDataType.String }],
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

        using var col = factory.CreateAndOpen(path, schema);
        float[] vec = [1f, 0f, 0f, 0f];
        foreach (var id in new[] { "a", "b", "c" })
        {
            col.Insert(ZVecDoc.Create(id,
                    fields: new Dictionary<string, object> { ["title"] = id },
                    denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
                .IsSuccess.Should().BeTrue();
        }

        using (var it = col.Iterate(new ZVecIterateOptions { IncludeVector = false }))
        {
            using var enumerator = it.GetEnumerator();
            enumerator.MoveNext().Should().BeTrue();
            var ids = new List<string> { enumerator.Current.Id };

            col.Insert(ZVecDoc.Create("d",
                    fields: new Dictionary<string, object> { ["title"] = "d" },
                    denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
                .IsSuccess.Should().BeTrue();

            while (enumerator.MoveNext())
                ids.Add(enumerator.Current.Id);

            ids.OrderBy(x => x, StringComparer.Ordinal).Should().Equal("a", "b", "c");
            ids.Should().NotContain("d");
        }
    }

    [Fact]
    public void HnswRabitq_Create_Still_ThrowsNotSupported()
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64)
        {
            Assert.Skip("HNSW-RaBitQ C API gap is asserted on x64.");
        }

        EnsureFactory();
        var act = () =>
        {
            using var _ = new NativeIndexParamBuilder(new ZVecHnswRabitqIndexParam());
        };
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*HNSW_RABITQ*");
    }

    [Fact]
#pragma warning disable CS0618
    public void QueryGroupBy_Execute_Still_ThrowsNotSupported()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_gb";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "group_by_blocked",
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

        using var col = factory.CreateAndOpen(path, schema);
        var gq = new ZVecGroupByQuery
        {
            GroupByField = "category",
            GroupSize = 1,
            Topk = 1,
            Query = new ZVecQuery
            {
                FieldName = "embedding",
                Vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
            }
        };

        var act = () => col.QueryGroupBy(gq);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*zvec_collection_group_by*");
    }
#pragma warning restore CS0618

    public void Dispose()
    {
        _factory?.Shutdown();
        TryDelete(_testPath);
        foreach (var p in _extraPaths)
            TryDelete(p);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
