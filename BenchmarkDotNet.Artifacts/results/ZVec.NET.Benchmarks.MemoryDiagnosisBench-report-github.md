```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8894)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method                   | Mean       | Error     | StdDev    | Gen0   | Allocated |
|------------------------- |-----------:|----------:|----------:|-------:|----------:|
| Query_768Dim             | 3,623.4 μs | 176.29 μs | 258.40 μs |      - |    7054 B |
| Query_768Dim_WithVectors | 3,786.1 μs | 181.41 μs | 265.91 μs | 7.8125 |   41534 B |
| Fetch_ScalarOnly         |   256.4 μs |  15.38 μs |  22.54 μs |      - |     968 B |
