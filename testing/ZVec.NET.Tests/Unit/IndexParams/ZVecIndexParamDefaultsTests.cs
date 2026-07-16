using FluentAssertions;

namespace ZVec.NET.Tests.Unit.IndexParams;

public class ZVecIndexParamDefaultsTests
{
    [Fact]
    public void ZVecHnswIndexParam_Defaults()
    {
        var p = new ZVecHnswIndexParam();
        p.MetricType.Should().Be(ZVecMetricType.Cosine);
        p.M.Should().Be(ZVecDefaults.Hnsw.M);
        p.EfConstruction.Should().Be(ZVecDefaults.Hnsw.EfConstruction);
        p.QuantizeType.Should().Be(ZVecQuantizeType.Undefined);
    }

    [Fact]
    public void ZVecHnswRabitqIndexParam_Defaults()
    {
        var p = new ZVecHnswRabitqIndexParam();
        p.MetricType.Should().Be(ZVecMetricType.Cosine);
        p.M.Should().Be(ZVecDefaults.HnswRabitq.M);
        p.EfConstruction.Should().Be(ZVecDefaults.HnswRabitq.EfConstruction);
        p.TotalBits.Should().Be(ZVecDefaults.HnswRabitq.TotalBits);
        p.NumClusters.Should().Be(ZVecDefaults.HnswRabitq.NumClusters);
        p.SampleCount.Should().Be(ZVecDefaults.HnswRabitq.SampleCount);
    }

    [Fact]
    public void ZVecIvfIndexParam_Defaults()
    {
        var p = new ZVecIvfIndexParam();
        p.MetricType.Should().Be(ZVecMetricType.L2);
        p.CentroidsNum.Should().Be(ZVecDefaults.Ivf.CentroidsNum);
        p.Nlist.Should().Be(ZVecDefaults.Ivf.Nlist);
        p.Nprobe.Should().Be(ZVecDefaults.Ivf.Nprobe);
        p.QuantizeType.Should().Be(ZVecQuantizeType.Undefined);
    }

    [Fact]
    public void ZVecFlatIndexParam_Defaults()
    {
        var p = new ZVecFlatIndexParam();
        p.MetricType.Should().Be(ZVecMetricType.L2);
        p.QuantizeType.Should().Be(ZVecQuantizeType.Undefined);
    }

    [Fact]
    public void ZVecDiskAnnIndexParam_Defaults()
    {
        var p = new ZVecDiskAnnIndexParam();
        p.MetricType.Should().Be(ZVecMetricType.L2);
        p.MaxDegree.Should().Be(ZVecDefaults.DiskAnn.MaxDegree);
        p.ListSize.Should().Be(ZVecDefaults.DiskAnn.ListSize);
        p.PqChunkNum.Should().Be(ZVecDefaults.DiskAnn.PqChunkNum);
        p.QuantizeType.Should().Be(ZVecQuantizeType.Undefined);
    }

    [Fact]
    public void ZVecVamanaIndexParam_Defaults()
    {
        var p = new ZVecVamanaIndexParam();
        p.MetricType.Should().Be(ZVecMetricType.L2);
        p.MaxDegree.Should().Be(ZVecDefaults.Vamana.MaxDegree);
        p.SearchListSize.Should().Be(ZVecDefaults.Vamana.SearchListSize);
        p.Alpha.Should().Be(ZVecDefaults.Vamana.Alpha);
        p.SaturateGraph.Should().BeFalse();
        p.UseContiguousMemory.Should().BeFalse();
        p.UseIdMap.Should().BeFalse();
        p.QuantizeType.Should().Be(ZVecQuantizeType.Undefined);
    }

    [Fact]
    public void ZVecFtsIndexParam_Defaults()
    {
        var p = new ZVecFtsIndexParam();
        p.Tokenizer.Should().Be(ZVecDefaults.Fts.Tokenizer);
        p.Filters.Should().Equal(ZVecDefaults.Fts.Filters);
        p.ExtraParams.Should().BeNull();
    }

    [Fact]
    public void ZVecInvertIndexParam_Defaults()
    {
        var p = new ZVecInvertIndexParam();
        p.EnableRangeOptimization.Should().BeFalse();
        p.EnableExtendedWildcard.Should().BeFalse();
    }
}
