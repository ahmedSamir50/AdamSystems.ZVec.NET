using BenchmarkDotNet.Running;

namespace AdamSystems.ZVec.NET.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ZVecPerformanceBenchmarks>();
    }
}
