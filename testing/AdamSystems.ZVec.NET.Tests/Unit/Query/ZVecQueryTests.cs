using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Query;

public class ZVecQueryTests
{
    [Fact]
    public void ZVecQuery_RequiresFieldName()
    {
        var q = new ZVecQuery { FieldName = "embedding" };
        q.Vector.Should().BeNull();
        q.FieldName.Should().Be("embedding");
        q.SparseVector.Should().BeNull();
        q.DocumentId.Should().BeNull();
        q.Fts.Should().BeNull();
        q.QueryParams.Should().BeNull();
    }

    [Fact]
    public void ZVecHnswQueryParams_EfSearch()
    {
        var p = new ZVecHnswQueryParams { EfSearch = 64 };
        p.EfSearch.Should().Be(64);
    }

    [Fact]
    public void ZVecIvfQueryParams_Nprobe()
    {
        var p = new ZVecIvfQueryParams { Nprobe = 16 };
        p.Nprobe.Should().Be(16);
    }

    [Fact]
    public void ZVecFtsQuery_DefaultOperator_IsOr()
    {
        var fts = new ZVecFtsQuery();
        fts.DefaultOperator.Should().Be(ZVecFtsDefaultOperator.Or);
        fts.MatchString.Should().BeNull();
        fts.QueryString.Should().BeNull();
    }

    [Fact]
    public void ZVecGroupByQuery_Defaults()
    {
        var g = new ZVecGroupByQuery
        {
            Query = new ZVecQuery { FieldName = "embedding" },
            GroupByField = "category"
        };
        g.GroupSize.Should().Be(ZVecDefaults.Query.GroupSize);
        g.Topk.Should().Be(ZVecDefaults.Query.Topk);
        g.Filter.Should().BeNull();
    }
}
