using FluentAssertions;
using ZVec.NET.Exceptions;
using ZVec.NET.Mapping;
using ZVec.NET.Tests.Helpers;

namespace ZVec.NET.Tests.Unit.Mapping;

public class ZVecCollectionOfTTests
{
    [Fact]
    public void Untyped_ExposesInnerCollection()
    {
        var inner = new RecordingCollection { Schema = SchemaMatchingProduct() };
        var typed = new ZVecCollection<Product>(inner);
        typed.Untyped.Should().BeSameAs(inner);
        typed.Path.Should().Be(inner.Path);
    }

    [Fact]
    public void Insert_MapsToDoc_AndCallsInner()
    {
        var inner = new RecordingCollection { Schema = SchemaMatchingProduct() };
        var typed = new ZVecCollection<Product>(inner);
        var product = SampleProduct();

        typed.Insert(product).IsSuccess.Should().BeTrue();

        inner.InsertedDocs.Should().ContainSingle();
        inner.InsertedDocs[0].Id.Should().Be("p1");
        inner.InsertedDocs[0].Fields["Category"].Should().Be("fiction");
        inner.InsertedDocs[0].DenseVectors["Embedding"].ToArray().Should().Equal(0.1f, 0.2f, 0.3f, 0.4f);
    }

    [Fact]
    public void Insert_WhenSchemaMissingField_ThrowsMismatch()
    {
        var inner = new RecordingCollection
        {
            Schema = new ZVecCollectionSchema
            {
                Name = "Product",
                Fields = [new ZVecFieldSchema { Name = "Title", DataType = ZVecDataType.String }],
                Vectors =
                [
                    new ZVecVectorSchema
                    {
                        Name = "Embedding",
                        DataType = ZVecDataType.VectorFp32,
                        Dimension = 4,
                        IndexParam = new ZVecFlatIndexParam()
                    }
                ]
            }
        };
        var typed = new ZVecCollection<Product>(inner);
        var act = () => typed.Insert(SampleProduct());
        act.Should().Throw<ZVecSchemaMismatchException>();
    }

    [Fact]
    public void Update_Upsert_Delete_DeleteByFilter_Forward()
    {
        var inner = new RecordingCollection { Schema = SchemaMatchingProduct() };
        var typed = new ZVecCollection<Product>(inner);

        typed.Update(SampleProduct());
        typed.Upsert(SampleProduct());
        typed.Delete("p1");
        typed.DeleteByFilter(p => p.Category == "fiction");

        inner.UpdatedDocs.Should().ContainSingle();
        inner.UpsertedDocs.Should().ContainSingle();
        inner.DeletedIds.Should().Equal("p1");
        inner.DeleteFilters.Should().Equal("Category = \"fiction\"");
    }

    [Fact]
    public void Fetch_MapsFromDoc()
    {
        var inner = new RecordingCollection
        {
            Schema = SchemaMatchingProduct(),
            FetchResult = ZVecMapper.ToDoc(SampleProduct())
        };
        var typed = new ZVecCollection<Product>(inner);
        var fetched = typed.Fetch("p1", includeVector: true);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Book");
    }

    [Fact]
    public void Query_UsesVectorStorageName_AndExpressionFilter()
    {
        var inner = new RecordingCollection
        {
            Schema = SchemaMatchingProduct(),
            QueryResult = [ZVecMapper.ToDoc(SampleProduct())]
        };
        var typed = new ZVecCollection<Product>(inner);
        var vec = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

        var hits = typed.Query(p => p.Embedding, vec, topK: 7, filter: p => p.Category == "fiction", includeVector: false);

        hits.Should().ContainSingle(h => h.Record.Id == "p1");
        inner.Queries.Should().ContainSingle();
        inner.Queries[0].Query.FieldName.Should().Be("Embedding");
        inner.Queries[0].Topk.Should().Be(7);
        inner.Queries[0].Filter.Should().Be("Category = \"fiction\"");
        inner.Queries[0].IncludeVector.Should().BeFalse();
    }

    [Fact]
    public void Ddl_ForwardsStorageNames()
    {
        var inner = new RecordingCollection { Schema = SchemaMatchingProduct() };
        var typed = new ZVecCollection<Product>(inner);

        typed.AddColumn(p => p.Year);
        typed.DropColumn(p => p.Year);
        typed.AlterColumn(p => p.Year, newName: "PublishYear");
        typed.CreateIndex(p => p.Year, new ZVecInvertIndexParam());
        typed.DropIndex(p => p.Year);
        typed.Optimize();

        inner.AddedColumns.Should().ContainSingle(c => c.Name == "Year" && c.Nullable);
        inner.DroppedColumns.Should().Equal("Year");
        inner.AlteredColumns.Should().ContainSingle(a => a.Column == "Year" && a.NewName == "PublishYear");
        inner.CreatedIndexes.Should().ContainSingle(c => c.Column == "Year");
        inner.DroppedIndexes.Should().Equal("Year");
        inner.OptimizeCalls.Should().Be(1);
    }

    [Fact]
    public void EnsureSchema_AddsMissingNumericField()
    {
        var inner = new RecordingCollection { Schema = SchemaMatchingProductWithoutYear() };
        var typed = new ZVecCollection<Product>(inner);
        typed.EnsureSchema();
        inner.AddedColumns.Should().ContainSingle(c => c.Name == "Year");
    }

    private static Product SampleProduct() => new()
    {
        Id = "p1",
        Title = "Book",
        Category = "fiction",
        Year = 2024,
        Embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
    };

    private static ZVecCollectionSchema SchemaMatchingProduct() =>
        ZVecCollectionSchemaBuilder.From<Product>().Build();

    private static ZVecCollectionSchema SchemaMatchingProductWithoutYear()
    {
        var full = SchemaMatchingProduct();
        return new ZVecCollectionSchema
        {
            Name = full.Name,
            Fields = full.Fields.Where(f => f.Name != "Year").ToArray(),
            Vectors = full.Vectors
        };
    }

    private sealed class Product
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public int Year { get; set; }
        [ZVecVector(4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
