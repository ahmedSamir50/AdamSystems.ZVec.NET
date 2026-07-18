using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZVec.NET.DependencyInjection;
using ZVec.NET.Mapping;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// After CreateAndOpen → insert → dispose, plain <see cref="IZvecFactory.Open"/>
/// must bind on-disk schema and return scalar fields (not Id/Score only).
/// </summary>
public class OpenSchemaBindingIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;

    public OpenSchemaBindingIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_open_schema_{Guid.NewGuid():N}");
    }

    private static ZVecCollectionSchema CreateSchema() => new()
    {
        Name = "open_schema_binding",
        Fields =
        [
            new ZVecFieldSchema { Name = "title", DataType = ZVecDataType.String },
            new ZVecFieldSchema { Name = "category", DataType = ZVecDataType.String }
        ],
        Vectors =
        [
            new ZVecVectorSchema
            {
                Name = "embedding",
                DataType = ZVecDataType.VectorFp32,
                Dimension = 4,
                IndexParam = new ZVecHnswIndexParam()
            }
        ]
    };

    private IZvecFactory EnsureFactory()
    {
        _fixture.SkipIfNotAvailable();
        _factory ??= new ZVecFactory();
        if (!_factory.IsInitialized)
            _factory.Initialize();
        return _factory;
    }

    private void SeedUntypedCollection(float[] vector)
    {
        var factory = EnsureFactory();
        var schema = CreateSchema();
        using var col = factory.CreateAndOpen(_testPath, schema);
        var doc = ZVecDoc.Create(
            "persist1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object>
            {
                ["title"] = "persisted-title",
                ["category"] = "cat-a"
            });
        col.Insert(doc).IsSuccess.Should().BeTrue();

        var control = col.Fetch("persist1", includeVector: false);
        control.Should().NotBeNull();
        control!.Fields["title"].Should().Be("persisted-title");
        control.Fields["category"].Should().Be("cat-a");
    }

    private static void AssertBoundSchemaShape(
        ZVecCollectionSchema? schema,
        string titleField,
        string categoryField,
        string vectorField)
    {
        schema.Should().NotBeNull();
        schema!.Fields.Should().Contain(f => f.Name == titleField && f.DataType == ZVecDataType.String);
        schema.Fields.Should().Contain(f => f.Name == categoryField && f.DataType == ZVecDataType.String);
        schema.Vectors.Should().ContainSingle(v =>
            v.Name == vectorField &&
            v.DataType == ZVecDataType.VectorFp32 &&
            v.Dimension == 4);
    }

    [Fact]
    public void Open_Reopen_Fetch_ReturnsScalarFields()
    {
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        SeedUntypedCollection(vector);

        using var reopened = EnsureFactory().Open(_testPath);
        AssertBoundSchemaShape(reopened.Schema, "title", "category", "embedding");

        var fetched = reopened.Fetch("persist1", includeVector: false);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be("persist1");
        fetched.Fields.Should().ContainKey("title");
        fetched.Fields["title"].Should().Be("persisted-title");
        fetched.Fields["category"].Should().Be("cat-a");
    }

    [Fact]
    public void Open_Reopen_Query_ReturnsScalarFields()
    {
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        SeedUntypedCollection(vector);

        using var reopened = EnsureFactory().Open(_testPath);
        AssertBoundSchemaShape(reopened.Schema, "title", "category", "embedding");

        var hits = reopened.Query(
            new ZVecQuery { FieldName = "embedding", Vector = vector },
            topk: 1,
            includeVector: false);

        hits.Should().ContainSingle();
        hits[0].Id.Should().Be("persist1");
        hits[0].Score.Should().BeGreaterThanOrEqualTo(0f);
        hits[0].Fields.Should().ContainKey("title");
        hits[0].Fields["title"].Should().Be("persisted-title");
    }

    [Fact]
    public void TypedWrap_Open_Reopen_Query_ReturnsMappedProperties()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var schema = ZVecCollectionSchemaBuilder.From<Product>().Build();
        using (var untyped = _factory.CreateAndOpen(_testPath, schema))
        {
            var typed = new ZVecCollection<Product>(untyped);
            typed.Insert(new Product
            {
                Id = "p1",
                Title = "Book",
                Category = "fiction",
                Embedding = vector
            }).IsSuccess.Should().BeTrue();

            var control = typed.Fetch("p1", includeVector: false);
            control!.Title.Should().Be("Book");
        }

        using var opened = _factory.Open(_testPath);
        AssertBoundSchemaShape(opened.Schema, "Title", "Category", "Embedding");

        var reopened = new ZVecCollection<Product>(opened);
        var hits = reopened.Query(p => p.Embedding, vector, topK: 1, includeVector: false);

        hits.Should().ContainSingle();
        hits[0].Record.Id.Should().Be("p1");
        hits[0].Record.Title.Should().Be("Book");
        hits[0].Record.Category.Should().Be("fiction");
    }

    [Fact]
    public void TypedDI_CreateFalse_Reopen_ReturnsScalars()
    {
        _fixture.SkipIfNotAvailable();
        var path = Path.Combine(Path.GetTempPath(), $"zvec_open_schema_di_{Guid.NewGuid():N}");
        var previous = ZVecDefaults.Version.BypassAbiCheck;
        try
        {
            ZVecDefaults.Version.BypassAbiCheck = true;
            var vector = new float[] { 0.2f, 0.1f, 0.0f, 0.4f };

            {
                var services = new ServiceCollection();
                services.AddZVec();
                services.AddZVecCollection<Product>(o =>
                {
                    o.Path = path;
                    o.Create = true;
                });

                using var sp = services.BuildServiceProvider();
                var typed = sp.GetRequiredService<IZvecCollection<Product>>();
                typed.Insert(new Product
                {
                    Id = "di-reopen",
                    Title = "DI-Reopen",
                    Category = "cat-di",
                    Embedding = vector
                }).IsSuccess.Should().BeTrue();
            }

            {
                var services = new ServiceCollection();
                services.AddZVec();
                services.AddZVecCollection<Product>(o =>
                {
                    o.Path = path;
                    o.Create = false;
                });

                using var sp = services.BuildServiceProvider();
                var typed = sp.GetRequiredService<IZvecCollection<Product>>();
                typed.Schema.Should().NotBeNull();
                AssertBoundSchemaShape(typed.Schema, "Title", "Category", "Embedding");

                var fetched = typed.Fetch("di-reopen", includeVector: false);
                fetched.Should().NotBeNull();
                fetched!.Title.Should().Be("DI-Reopen");
                fetched.Category.Should().Be("cat-di");
            }
        }
        finally
        {
            ZVecDefaults.Version.BypassAbiCheck = previous;
            if (Directory.Exists(path))
            {
                try { Directory.Delete(path, true); }
                catch { /* ignore cleanup */ }
            }
        }
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

    private sealed class Product
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        [ZVecVector(4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
