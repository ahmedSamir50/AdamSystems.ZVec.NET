```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method               | Mean     | Error    | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |---------:|---------:|----------:|------:|--------:|-------:|----------:|------------:|
| Query_ReadOnlyMemory | 3.433 ms | 5.084 ms | 0.2787 ms |  1.00 |    0.10 | 7.8125 |  44.35 KB |        1.00 |
| Query_ExplicitCopy   | 3.522 ms | 7.172 ms | 0.3931 ms |  1.03 |    0.13 | 7.8125 |  47.37 KB |        1.07 |
