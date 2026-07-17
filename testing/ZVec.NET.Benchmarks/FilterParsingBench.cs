using BenchmarkDotNet.Attributes;
using ZVec.NET.Query;

namespace ZVec.NET.Benchmarks;

[MemoryDiagnoser]
public class FilterParsingBench
{
    private ZVecFilterBuilder _simpleFilter = null!;
    private ZVecFilterBuilder _compoundFilter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simpleFilter = ZVecFilterBuilder.Create()
            .Where("publish_year", ZVecCompareOp.Gt, 1936);

        _compoundFilter = ZVecFilterBuilder.Create()
            .Where("publish_year", ZVecCompareOp.Gt, 1936)
            .And(b => b.ContainAny("category", "fiction", "romance"));
    }

    [Benchmark]
    public string Build_SimpleFilter()
    {
        return _simpleFilter.Build();
    }

    [Benchmark]
    public string Build_CompoundFilter()
    {
        return _compoundFilter.Build();
    }
}
