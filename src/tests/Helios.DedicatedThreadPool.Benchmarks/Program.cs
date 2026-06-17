using BenchmarkDotNet.Running;
using Helios.Concurrency.Benchmarks;

// Run all methods in ThroughputBenchmarks; forward args so callers can pass e.g. --job dry.
BenchmarkRunner.Run<ThroughputBenchmarks>(args: args);
