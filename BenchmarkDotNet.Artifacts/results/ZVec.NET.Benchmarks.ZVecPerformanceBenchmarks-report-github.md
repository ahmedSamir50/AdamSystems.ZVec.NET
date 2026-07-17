```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method         | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------- |-----------:|----------:|----------:|-------:|----------:|
| InsertDocument | 3,282.8 ns | 127.46 ns | 182.80 ns | 0.1106 |     528 B |
| QueryVector    |   515.9 ns |  17.57 ns |  26.30 ns | 0.0439 |     208 B |
