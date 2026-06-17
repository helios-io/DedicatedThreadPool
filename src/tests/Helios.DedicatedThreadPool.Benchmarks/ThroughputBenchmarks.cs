using System;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Helios.Concurrency.Benchmarks;

/// <summary>
/// Compares Helios DedicatedThreadPool throughput against the .NET ThreadPool
/// by queueing 100 K no-op work items and waiting for all to complete.
/// </summary>
[MemoryDiagnoser]
public class ThroughputBenchmarks
{
    private const int WorkItems = 100_000;

    private DedicatedThreadPool _heliosPool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _heliosPool = new DedicatedThreadPool(
            new DedicatedThreadPoolSettings(Environment.ProcessorCount));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _heliosPool.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void DotNetThreadPool()
    {
        using var done = new ManualResetEventSlim(false);
        int remaining = WorkItems;
        for (int i = 0; i < WorkItems; i++)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (Interlocked.Decrement(ref remaining) == 0)
                    done.Set();
            });
        }
        done.Wait();
    }

    [Benchmark]
    public void HeliosPool()
    {
        using var done = new ManualResetEventSlim(false);
        int remaining = WorkItems;
        for (int i = 0; i < WorkItems; i++)
        {
            _heliosPool.QueueUserWorkItem(() =>
            {
                if (Interlocked.Decrement(ref remaining) == 0)
                    done.Set();
            });
        }
        done.Wait();
    }
}
