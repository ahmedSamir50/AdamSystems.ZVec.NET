```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8894)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method               | Mean      | Error     | StdDev    | Gen0   | Allocated |
|--------------------- |----------:|----------:|----------:|-------:|----------:|
| Build_SimpleFilter   |  62.37 ns |  2.468 ns |  3.618 ns | 0.0272 |     128 B |
| Build_CompoundFilter | 221.17 ns | 11.171 ns | 16.720 ns | 0.1154 |     544 B |
