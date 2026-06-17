using System;
using System.Linq;
using BenchmarkDotNet.Running;
using Helios.Concurrency.Benchmarks;

// Discover all benchmark classes in this assembly.
// Add --filter * by default so unfiltered runs and CI (--job dry) both cover all classes.
var runArgs = args.Any(a => a.StartsWith("--filter", StringComparison.Ordinal))
    ? args
    : [.. args, "--filter", "*"];
BenchmarkSwitcher
    .FromAssembly(typeof(ThroughputBenchmarks).Assembly)
    .Run(runArgs);
