```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8894)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method               | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| Query_ReadOnlyMemory | 3.402 ms | 0.1467 ms | 0.2104 ms |  1.00 |    0.08 |   6.96 KB |        1.00 |
| Query_ExplicitCopy   | 3.415 ms | 0.3775 ms | 0.5167 ms |  1.01 |    0.16 |   9.99 KB |        1.43 |
