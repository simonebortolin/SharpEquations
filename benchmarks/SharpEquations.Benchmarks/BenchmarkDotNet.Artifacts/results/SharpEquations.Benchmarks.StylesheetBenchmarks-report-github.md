```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.114
  [Host]   : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean      | Error     | StdDev    | Gen0      | Allocated |
|-------------------------------- |----------:|----------:|----------:|----------:|----------:|
| Omml2MathML                     | 206.15 ms | 353.93 ms | 19.400 ms | 1666.6667 | 461.45 MB |
| Omml2MathMLXslCompiledTransform |  65.30 ms |  49.96 ms |  2.738 ms |  500.0000 | 156.44 MB |
| MathML2Omml                     | 140.78 ms |  13.86 ms |  0.760 ms | 1000.0000 | 261.16 MB |
| MathML2OmmlXslCompiledTransform |  72.10 ms | 114.18 ms |  6.259 ms |  500.0000 | 136.35 MB |
| Mtef2MathML                     |  85.12 ms |  34.89 ms |  1.913 ms |  222.2222 |   79.9 MB |
| Mtef2MathMLXslCompiledTransform |  35.28 ms |  18.50 ms |  1.014 ms |         - |  20.89 MB |
