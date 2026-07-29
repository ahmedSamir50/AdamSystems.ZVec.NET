```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8894)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]    : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  MediumRun : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method         | Mean       | Error     | StdDev    | Gen0   | Allocated |
|--------------- |-----------:|----------:|----------:|-------:|----------:|
| InsertDocument | 3,733.1 ns | 136.00 ns | 195.05 ns | 0.1221 |     592 B |
| QueryVector    |   567.2 ns |  22.30 ns |  31.27 ns | 0.0572 |     272 B |
