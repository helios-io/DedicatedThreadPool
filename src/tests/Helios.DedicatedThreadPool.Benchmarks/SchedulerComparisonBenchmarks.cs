using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Pipelines.Sockets.Unofficial;

namespace Helios.Concurrency.Benchmarks;

/// <summary>
/// Three-way throughput comparison: current Helios pool, .NET ThreadPool, and the
/// lock-based DedicatedThreadPoolPipeScheduler from Pipelines.Sockets.Unofficial.
/// Run at both default (ProcessorCount) and constrained (2-thread) worker counts to
/// surface the lock-contention penalty on narrow machines (StackExchange.Redis #3060 scenario).
/// WorkerCount=0 means Environment.ProcessorCount.
/// </summary>
[MemoryDiagnoser]
public class SchedulerComparisonBenchmarks
{
    private const int WorkItems = 100_000;

    // 0 = Environment.ProcessorCount (default), 2 = constrained
    [Params(0, 2)]
    public int WorkerCount;

    private DedicatedThreadPool _heliosPool = null!;
    private DedicatedThreadPoolPipeScheduler _pipeScheduler = null!;

    [GlobalSetup]
    public void Setup()
    {
        int count = WorkerCount == 0 ? Environment.ProcessorCount : WorkerCount;
        _heliosPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(count));
        _pipeScheduler = new DedicatedThreadPoolPipeScheduler("bench", workerCount: count);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _heliosPool.Dispose();
        _pipeScheduler.Dispose();
    }

    // .NET ThreadPool is not configurable to a fixed worker count without SetMaxThreads,
    // so it runs at its default thread count in both param configurations.
    [Benchmark(Baseline = true)]
    public void DotNetThreadPool()
    {
        using var done = new ManualResetEventSlim(false);
        int remaining = WorkItems;
        for (int i = 0; i < WorkItems; i++)
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                if (Interlocked.Decrement(ref remaining) == 0)
                    done.Set();
            }, (object?)null, preferLocal: false);
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

    [Benchmark]
    public void PipeScheduler()
    {
        using var done = new ManualResetEventSlim(false);
        int remaining = WorkItems;
        for (int i = 0; i < WorkItems; i++)
        {
            _pipeScheduler.Schedule(_ =>
            {
                if (Interlocked.Decrement(ref remaining) == 0)
                    done.Set();
            }, null);
        }
        done.Wait();
    }
}
