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

            // Keyed untyped shares the same underlying collection
            var keyed = sp.GetRequiredKeyedService<IZvecCollection>("Product");
            keyed.Should().BeSameAs(typed.Untyped);

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

    // Dynamic/string API coverage remains in E12–E18 suites (CrudLifecycle, FilterBuilder, SchemaEvolution, etc.).

    [Fact]
    public async Task Typed_Update_Upsert_Delete_QueryAsync_RoundTrip()
    {
        Setup();
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        await _collection!.InsertAsync(new Product
        {
            Id = "u1",
            Title = "One",
            Category = "a",
            Embedding = vector
        });

        await _collection.UpdateAsync(new Product
        {
            Id = "u1",
            Title = "Two",
            Category = "a",
            Embedding = vector
        });
        (await _collection.FetchAsync("u1"))!.Title.Should().Be("Two");

        await _collection.UpsertAsync(new Product
        {
            Id = "u2",
            Title = "Upserted",
            Category = "b",
            Embedding = vector
        });
        (await _collection.FetchAsync("u2"))!.Title.Should().Be("Upserted");

        var hits = await _collection.QueryAsync(
            p => p.Embedding,
            vector,
            topK: 5,
            filter: p => p.Category == "b");
        hits.Should().Contain(h => h.Record.Id == "u2");

        await _collection.DeleteByFilterAsync(p => p.Category == "b");
        (await _collection.FetchAsync("u2")).Should().BeNull();

        await _collection.DeleteAsync("u1");
        (await _collection.FetchAsync("u1")).Should().BeNull();
    }

    [Fact]
    public async Task Typed_Ddl_AddIndex_Optimize_Async()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        var path = Path.Combine(Path.GetTempPath(), $"zvec_typed_ddl_{Guid.NewGuid():N}");
        var untyped = _factory.CreateAndOpen(path, ZVecCollectionSchemaBuilder.From<Product>().Build());
        var extended = new ZVecCollection<ExtendedProduct>(untyped);

        await extended.EnsureSchemaAsync();
        await extended.CreateIndexAsync(p => p.Year, new ZVecInvertIndexParam());
        await extended.DropIndexAsync(p => p.Year);
        await extended.OptimizeAsync();
        await extended.DropColumnAsync(p => p.Year);
        extended.Schema!.Fields.Select(f => f.Name).Should().NotContain("Year");

        extended.Dispose();
        try { Directory.Delete(path, true); } catch { /* best effort */ }
    }

    [Fact]
    public void Typed_AlterColumn_RenamesStorage()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        var path = Path.Combine(Path.GetTempPath(), $"zvec_typed_alter_{Guid.NewGuid():N}");
        var untyped = _factory.CreateAndOpen(path, ZVecCollectionSchemaBuilder.From<Product>().Build());
        var extended = new ZVecCollection<ExtendedProduct>(untyped);
        extended.EnsureSchema();
        extended.AlterColumn(p => p.Year, newName: "PubYear");
        extended.Schema!.Fields.Should().Contain(f => f.Name == "PubYear");
        extended.Untyped.DropColumn("PubYear");

        extended.Dispose();
        try { Directory.Delete(path, true); } catch { /* best effort */ }
    }

    [Fact]
    public void Typed_AfterDrop_MapperIgnoresLeftoverWhenReadingLeanType()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        var path = Path.Combine(Path.GetTempPath(), $"zvec_typed_leftover_{Guid.NewGuid():N}");
        var untyped = _factory.CreateAndOpen(path, ZVecCollectionSchemaBuilder.From<ExtendedProduct>().Build());
        var extended = new ZVecCollection<ExtendedProduct>(untyped);
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        extended.Insert(new ExtendedProduct
        {
            Id = "L1",
            Title = "T",
            Category = "c",
            Year = 1999,
            Embedding = vector
        }).IsSuccess.Should().BeTrue();

        // Native still has Year; lean Product type has no Year — FromDoc ignores leftover
        var lean = new ZVecCollection<Product>(extended.Untyped);
        var fetched = lean.Fetch("L1", includeVector: false);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("T");

        extended.Dispose();
        try { Directory.Delete(path, true); } catch { /* best effort */ }
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
