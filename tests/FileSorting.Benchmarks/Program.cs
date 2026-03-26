using BenchmarkDotNet.Running;
using FileSorting.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(SortingBenchmarks).Assembly).Run(args);
