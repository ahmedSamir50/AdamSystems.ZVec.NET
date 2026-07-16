using BenchmarkDotNet.Running;

namespace ZVec.NET.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ZVecPerformanceBenchmarks>();
    }
}
