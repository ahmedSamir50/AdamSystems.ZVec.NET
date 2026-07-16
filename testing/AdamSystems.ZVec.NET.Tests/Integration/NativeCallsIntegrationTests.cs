using System;
using System.IO;
using AdamSystems.ZVec.NET.Exceptions;
using FluentAssertions;
using Xunit;

namespace AdamSystems.ZVec.NET.Tests.Integration;

[Collection("RealNativeCollection")]
public class NativeCallsIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testDir;
    private readonly ZVecFactory _factory;
    private readonly List<IZvecCollection> _collectionsToCleanup = new();

    public NativeCallsIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testDir = Path.Combine(Path.GetTempPath(), $"zvec-native-calls-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _factory = new ZVecFactory();
        _factory.Initialize();
    }

    public void Dispose()
    {
        foreach (var col in _collectionsToCleanup)
        {
            try { col.Dispose(); } catch { }
        }

        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        } catch { }
    }

    private IZvecCollection CreateCollection(string name)
    {
        var schema = new ZVecCollectionSchema
        {
            Name = name,
            Vectors = [new ZVecVectorSchema { Name = "vec", DataType = ZVecDataType.VectorFp32, Dimension = 2, IndexParam = new ZVecFlatIndexParam() }]
        };
        var col = _factory.CreateAndOpen(Path.Combine(_testDir, $"coll-{Guid.NewGuid()}"), schema);
        _collectionsToCleanup.Add(col);
        return col;
    }

    [Fact]
    public void AddColumn_AttemptsNativeCall()
    {
        if (!_fixture.IsRealNativeAvailable) return;
        var col = CreateCollection("test_add_col");
        var field = new ZVecFieldSchema { Name = "new_field", DataType = ZVecDataType.Int32, Nullable = true };
        
        var act = () => col.AddColumn(field);
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateIndex_AttemptsNativeCall()
    {
        if (!_fixture.IsRealNativeAvailable) return;
        var col = CreateCollection("test_create_idx");
        
        // ZVec API requires column to exist first, and must be numeric for InvertIndex
        col.AddColumn(new ZVecFieldSchema { Name = "index_field", DataType = ZVecDataType.Int32, Nullable = true });
        
        var act = () => col.CreateIndex("index_field", new ZVecInvertIndexParam());
        act.Should().NotThrow();
    }

    [Fact]
    public void Optimize_AttemptsNativeCall()
    {
        if (!_fixture.IsRealNativeAvailable) return;
        var col = CreateCollection("test_optimize");
        
        var act = () => col.Optimize();
        act.Should().NotThrow();
    }

    [Fact]
    public void Insert_AttemptsNativeCall()
    {
        if (!_fixture.IsRealNativeAvailable) return;
        var col = CreateCollection("test_insert");
        var doc = new ZVecDoc { Id = "doc1", DenseVectors = new Dictionary<string, ReadOnlyMemory<float>> { { "vec", new float[] { 1.0f, 2.0f } } } };
        
        var act = () => col.Insert(doc);
        act.Should().NotThrow();
    }

    [Fact]
    public void Fetch_AttemptsNativeCall()
    {
        if (!_fixture.IsRealNativeAvailable) return;
        var col = CreateCollection("test_fetch");
        
        var act = () => col.Fetch(["doc1"]);
        act.Should().NotThrow();
    }

    [Fact]
    public void Query_AttemptsNativeCall()
    {
        if (!_fixture.IsRealNativeAvailable) return;
        var col = CreateCollection("test_query");
        var q = new ZVecQuery { FieldName = "vec", Vector = new float[] { 1.0f, 2.0f } };
        
        var act = () => col.Query(q);
        act.Should().NotThrow();
    }
}
