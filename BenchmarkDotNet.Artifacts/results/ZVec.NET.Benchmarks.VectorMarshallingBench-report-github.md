```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method               | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| Query_ReadOnlyMemory | 4.289 ms | 0.8236 ms | 1.2328 ms |  1.08 |    0.42 |    6.9 KB |        1.00 |
| Query_ExplicitCopy   | 2.612 ms | 0.1014 ms | 0.1454 ms |  0.66 |    0.18 |   9.92 KB |        1.44 |
