using BenchmarkDotNet.Running;

namespace ZVec.NET.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(UpstreamEngineScaleBaseline.SummaryLine);
        Console.WriteLine($"Local binding suite: {BenchmarkEnvironment.WorkloadLabel}");
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
