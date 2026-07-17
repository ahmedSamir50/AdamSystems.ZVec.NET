using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZVec.NET.DependencyInjection;
using ZVec.NET.Exceptions;
using ZVec.NET.Mapping;

namespace ZVec.NET.Tests.Integration;

public class TypedOdmIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection<Product>? _collection;

    public TypedOdmIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_typed_odm_{Guid.NewGuid():N}");
    }

    private void Setup()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        var schema = ZVecCollectionSchemaBuilder.From<Product>().Build();
        var untyped = _factory.CreateAndOpen(_testPath, schema);
        _collection = new ZVecCollection<Product>(untyped);
    }

    [Fact]
    public void Typed_Insert_Fetch_Query_RoundTrip()
    {
        Setup();
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var product = new Product
        {
            Id = "p1",
            Title = "Book",
            Category = "fiction",
            Embedding = vector
        };

        _collection!.Insert(product).IsSuccess.Should().BeTrue();

        var fetched = _collection.Fetch("p1", includeVector: true);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Book");
        fetched.Category.Should().Be("fiction");
        fetched.Embedding.ToArray().Should().Equal(vector);

        var hits = _collection.Query(
            p => p.Embedding,
            vector,
            topK: 5,
            filter: p => p.Category == "fiction");

        hits.Should().ContainSingle(h => h.Record.Id == "p1");
    }

    [Fact]
    public void Typed_EnsureSchema_AddsMissingScalarColumn()
    {
        Setup();
        // Create base collection without Publisher, then wrap ExtendedProduct after AddColumn via EnsureSchema
        _collection!.Dispose();
        _collection = null;

        var baseSchema = ZVecCollectionSchemaBuilder.From<Product>().Build();
        var untyped = _factory!.CreateAndOpen(
            Path.Combine(Path.GetTempPath(), $"zvec_typed_ensure_{Guid.NewGuid():N}"),
            baseSchema);

        // Rebuild typed collection against same physical schema shape as ExtendedProduct minus Publisher
        var extended = new ZVecCollection<ExtendedProduct>(untyped);
        extended.EnsureSchema();
        extended.Schema!.Fields.Select(f => f.Name).Should().Contain("Year");

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        extended.Insert(new ExtendedProduct
        {
            Id = "e1",
            Title = "T",
            Category = "c",
            Year = 2024,
            Embedding = vector
        }).IsSuccess.Should().BeTrue();

        var fetched = extended.Fetch("e1");
        fetched!.Year.Should().Be(2024);
        extended.Dispose();
    }

    [Fact]
    public void Typed_SchemaMismatch_Throws_WhenNativeMissingField()
    {
        Setup();
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        // Product schema is open; ExtendedProduct requires Year
        var wrong = new ZVecCollection<ExtendedProduct>(_collection!.Untyped);
        var act = () => wrong.Insert(new ExtendedProduct
        {
            Id = "x",
            Title = "t",
            Category = "c",
            Year = 2020,
            Embedding = vector
        });
        act.Should().Throw<ZVecSchemaMismatchException>();
    }

    [Fact]
    public void Typed_DropColumn_IsExplicit()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        var path = Path.Combine(Path.GetTempPath(), $"zvec_typed_drop_{Guid.NewGuid():N}");
        var untyped = _factory.CreateAndOpen(path, ZVecCollectionSchemaBuilder.From<Product>().Build());
        var extended = new ZVecCollection<ExtendedProduct>(untyped);
        extended.EnsureSchema();
        extended.Schema!.Fields.Select(f => f.Name).Should().Contain("Year");
        extended.DropColumn(p => p.Year);
        extended.Schema!.Fields.Select(f => f.Name).Should().NotContain("Year");
        extended.Dispose();
        try { Directory.Delete(path, true); } catch { /* best effort */ }
    }

    [Fact]
    public void Typed_DI_RegistersCollection()
    {
        _fixture.SkipIfNotAvailable();
        var previous = ZVecDefaults.Version.BypassAbiCheck;
        try
        {
            ZVecDefaults.Version.BypassAbiCheck = true;
            var services = new ServiceCollection();
            services.AddZVec();
            services.AddZVecCollection<Product>(o =>
            {
                o.Path = _testPath;
                o.EnableMmap = true;
                o.Create = true;
            });

            using var sp = services.BuildServiceProvider();
            var typed = sp.GetRequiredService<IZvecCollection<Product>>();
            typed.Should().NotBeNull();
            typed.Path.Should().Be(_testPath);

            var vector = new float[] { 0.2f, 0.1f, 0.0f, 0.4f };
            typed.Insert(new Product
            {
                Id = "di1",
                Title = "DI",
                Category = "test",
                Embedding = vector
            }).IsSuccess.Should().BeTrue();
        }
        finally
        {
            ZVecDefaults.Version.BypassAbiCheck = previous;
        }
    }

    public void Dispose()
    {
        _collection?.Dispose();
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try { Directory.Delete(_testPath, true); }
            catch { /* best effort */ }
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

    /// <summary>
    /// Extends <see cref="Product"/> with a numeric column — native AddColumn only allows basic numeric types.
    /// </summary>
    private sealed class ExtendedProduct
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public int Year { get; set; }
        [ZVecVector(4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
