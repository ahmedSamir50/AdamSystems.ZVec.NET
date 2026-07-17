```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method                 | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------------- |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Query_Sync             | 2,881.3 μs | 190.61 μs | 279.39 μs | 2,878.5 μs |  1.01 |    0.14 |    3 |      - |   6.82 KB |        1.00 |
| Query_Sync_WithVectors | 2,566.4 μs |  55.45 μs |  81.28 μs | 2,540.1 μs |  0.90 |    0.09 |    3 | 7.8125 |   40.5 KB |        5.93 |
| Query_WithFilter       | 1,426.3 μs |  29.36 μs |  40.19 μs | 1,423.1 μs |  0.50 |    0.05 |    2 |      - |   1.73 KB |        0.25 |
| Query_WarmTinyCorpus   |   511.5 μs | 141.60 μs | 207.55 μs |   389.5 μs |  0.18 |    0.07 |    1 | 1.4648 |   6.74 KB |        0.99 |
