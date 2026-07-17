```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]     : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.29 (8.0.2926.32403), X64 RyuJIT AVX2


```
| Method         | Mean       | Error    | StdDev    | Gen0   | Allocated |
|--------------- |-----------:|---------:|----------:|-------:|----------:|
| InsertDocument | 3,308.9 ns | 59.87 ns | 110.98 ns | 0.1030 |     496 B |
| QueryVector    |   516.3 ns | 10.39 ns |  26.46 ns | 0.0439 |     208 B |
