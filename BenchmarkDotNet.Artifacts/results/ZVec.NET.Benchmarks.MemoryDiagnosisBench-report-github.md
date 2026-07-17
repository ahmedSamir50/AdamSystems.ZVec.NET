```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method           | Mean       | Error     | StdDev   | Gen0   | Allocated |
|----------------- |-----------:|----------:|---------:|-------:|----------:|
| Query_768Dim     | 3,134.2 μs | 860.42 μs | 47.16 μs | 7.8125 |  44.35 KB |
| Fetch_ScalarOnly |   210.1 μs |  38.59 μs |  2.12 μs | 0.2441 |   1.22 KB |
