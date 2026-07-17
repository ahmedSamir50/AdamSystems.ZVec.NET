```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8875)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.110
  [Host]     : .NET 9.0.18 (9.0.1826.31522), X64 RyuJIT AVX2
  Job-WUTLVJ : .NET 9.0.18 (9.0.1826.31522), X64 RyuJIT AVX2

IterationCount=5  WarmupCount=1  

```
| Method                     | Mean         | Error         | StdDev       | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |-------------:|--------------:|-------------:|------:|--------:|-----:|-------:|----------:|------------:|
| Insert_Dynamic             |  66,593.3 ns |  13,679.59 ns |  3,552.55 ns | 1.002 |    0.07 |    3 | 0.2441 |    1376 B |        1.00 |
| Insert_Typed               |  55,883.5 ns |  17,598.27 ns |  2,723.35 ns | 0.841 |    0.05 |    3 | 0.3662 |    2144 B |        1.56 |
| Query_Dynamic              | 441,822.1 ns |  67,193.27 ns | 10,398.23 ns | 6.650 |    0.35 |    4 | 0.9766 |    6505 B |        4.73 |
| Query_Typed                | 381,269.2 ns | 392,284.44 ns | 60,706.45 ns | 5.738 |    0.86 |    4 | 1.4648 |    8792 B |        6.39 |
| QueryFilter_Dynamic        | 569,467.7 ns | 117,894.27 ns | 30,616.77 ns | 8.571 |    0.59 |    4 | 0.9766 |    6505 B |        4.73 |
| QueryFilter_Typed          | 540,043.7 ns |  83,435.76 ns | 21,668.00 ns | 8.128 |    0.49 |    4 | 1.9531 |    9463 B |        6.88 |
| Mapper_ToDoc               |     413.9 ns |     124.27 ns |     32.27 ns | 0.006 |    0.00 |    2 | 0.2174 |    1024 B |        0.74 |
| Mapper_FromDoc             |     193.9 ns |      33.29 ns |      8.65 ns | 0.003 |    0.00 |    1 | 0.0339 |     160 B |        0.12 |
| ExpressionFilter_Translate |     371.5 ns |      43.67 ns |      6.76 ns | 0.006 |    0.00 |    2 | 0.1254 |     592 B |        0.43 |
