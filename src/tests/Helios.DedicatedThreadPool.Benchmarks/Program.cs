using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using BenchmarkDotNet.Running;
using Helios.Concurrency;
using Helios.Concurrency.Benchmarks;

// Dedicated idle-CPU harness (Task 3a). Hosts ONLY the pool and measures process-wide
// Environment.CpuUsage over an idle window — in a dedicated process, process CPU ≈ pool CPU
// (no test-runner / sibling-test contamination, unlike the xUnit smoke). Resolves the parked
// process-wide-gauge methodology by isolating the *process*, not the thread.
//   Usage: dotnet run -c Release -- idle-cpu [threads] [idleMs] [warmupMs]
if (args.Length > 0 && args[0] == "idle-cpu")
{
    int threads  = args.Length > 1 && int.TryParse(args[1], out var t) ? t : Environment.ProcessorCount;
    int idleMs   = args.Length > 2 && int.TryParse(args[2], out var m) ? m : 3000;
    int warmupMs = args.Length > 3 && int.TryParse(args[3], out var w) ? w : 300;

    using var pool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(threads));
    Thread.Sleep(warmupMs); // let workers spin up and settle into their idle/park path

    var before = Environment.CpuUsage;
    var sw = Stopwatch.StartNew();
    Thread.Sleep(idleMs);
    sw.Stop();
    var usedMs    = (Environment.CpuUsage.TotalTime - before.TotalTime).TotalMilliseconds;
    var wallSec   = sw.Elapsed.TotalSeconds;
    var pctOneCore = 100.0 * (usedMs / 1000.0) / wallSec;

    Console.WriteLine(
        $"[idle-cpu] threads={threads} wall={wallSec:F2}s cpu={usedMs:F1}ms " +
        $"pctOfOneCore={pctOneCore:F2}% perThread={pctOneCore / threads:F3}%");
    return;
}

// Bursty-load CPU harness (Task 3a): the regime where Sleep(0)-spin-before-park actually costs —
// each work item wakes a parked worker, which runs it, finds the queue empty, spins, then re-parks.
// Trivial work per item, so CPU above ~0 is the wake/spin/park machinery, not the work.
//   Usage: dotnet run -c Release -- burst-cpu [threads] [intervalMs] [durationMs]
if (args.Length > 0 && args[0] == "burst-cpu")
{
    int threads    = args.Length > 1 && int.TryParse(args[1], out var t) ? t : Environment.ProcessorCount;
    int intervalMs = args.Length > 2 && int.TryParse(args[2], out var iv) ? iv : 10;
    int durationMs = args.Length > 3 && int.TryParse(args[3], out var d) ? d : 3000;

    using var pool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(threads));
    Thread.Sleep(300);

    long processed = 0;
    var before = Environment.CpuUsage;
    var sw = Stopwatch.StartNew();
    while (sw.ElapsedMilliseconds < durationMs)
    {
        pool.QueueUserWorkItem(() => Interlocked.Increment(ref processed));
        Thread.Sleep(intervalMs);
    }
    sw.Stop();
    var usedMs     = (Environment.CpuUsage.TotalTime - before.TotalTime).TotalMilliseconds;
    var wallSec    = sw.Elapsed.TotalSeconds;
    var pctOneCore = 100.0 * (usedMs / 1000.0) / wallSec;

    Console.WriteLine(
        $"[burst-cpu] threads={threads} interval={intervalMs}ms wall={wallSec:F2}s " +
        $"items={Interlocked.Read(ref processed)} cpu={usedMs:F1}ms pctOfOneCore={pctOneCore:F2}%");
    return;
}

// Default: BenchmarkDotNet. --filter * so unfiltered runs and CI (--job dry) cover all classes.
var runArgs = args.Any(a => a.StartsWith("--filter", StringComparison.Ordinal))
    ? args
    : [.. args, "--filter", "*"];
BenchmarkSwitcher
    .FromAssembly(typeof(ThroughputBenchmarks).Assembly)
    .Run(runArgs);
