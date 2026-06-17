using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Helios.Concurrency.Tests
{
    // Prevent parallel execution with other test classes so process-wide CPU and contention
    // readings are not contaminated by SpinWait/lock activity in sibling test classes.
    [CollectionDefinition("MeasurementTests", DisableParallelization = true)]
    public class MeasurementTestsCollection { }

    /// <summary>
    /// Phase 2.4 measurement scaffolding: idle-CPU, Monitor contention, and metrics stubs.
    /// Documents baseline behaviour of the *current* pool. Phase 3 gates (dae34f6d) will add
    /// stricter assertions once the rewritten pool is in place.
    /// </summary>
    [Collection("MeasurementTests")]
    public sealed class PoolMeasurementTests
    {
        private readonly ITestOutputHelper _output;

        public PoolMeasurementTests(ITestOutputHelper output) => _output = output;

        // -----------------------------------------------------------------
        // 1. Idle-CPU harness  (Environment.CpuUsage / Fix-it C.2)
        // -----------------------------------------------------------------

        [Fact(DisplayName = "Idle pool has near-zero CPU usage (Environment.CpuUsage harness)")]
        public void IdlePool_CpuUsage_IsNearZero()
        {
            const int numThreads = 4;
            const int idleMs = 2_000;

            using var pool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(numThreads));

            // Let the pool settle into idle before sampling.
            Thread.Sleep(200);

            var cpuBefore = Environment.CpuUsage;
            var tsStart = Stopwatch.GetTimestamp();

            Thread.Sleep(idleMs);

            var cpuAfter = Environment.CpuUsage;
            var wallSec = Stopwatch.GetElapsedTime(tsStart).TotalSeconds;

            var cpuDeltaSec = (cpuAfter.TotalTime - cpuBefore.TotalTime).TotalSeconds;
            // cpuFraction: fraction of one core (1.0 = one core 100% busy)
            var cpuFraction = cpuDeltaSec / wallSec;

            _output.WriteLine(
                $"[idle-CPU] wall={wallSec:F2}s  cpu_delta={cpuDeltaSec * 1000:F1}ms  " +
                $"cpu_fraction={cpuFraction:P1}  pool_threads={numThreads}");

            // Generous threshold: busy-spin would saturate a core (>=1.0/thread).
            // Current pool uses UnfairSemaphore so idle CPU should be near 0.
            // Phase 3 gate will tighten this (dae34f6d benchmark discipline).
            Assert.True(cpuFraction < 0.20,
                $"Idle CPU too high: {cpuFraction:P1} — pool may be busy-spinning ({numThreads} threads)");
        }

        // -----------------------------------------------------------------
        // 2. Monitor.LockContentionCount harness
        // -----------------------------------------------------------------

        [Fact(DisplayName = "Monitor contention under pool load is documented (current pool baseline)")]
        public void Pool_MonitorContention_UnderLoad_IsDocumented()
        {
            const int numThreads = 4;
            const int workItems = 50_000;

            var contentionBefore = Monitor.LockContentionCount;
            var done = new CountdownEvent(workItems);

            using var pool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(numThreads));
            for (int i = 0; i < workItems; i++)
                pool.QueueUserWorkItem(() => done.Signal());

            Assert.True(done.Wait(TimeSpan.FromSeconds(15)),
                "Pool did not process all work items within the 15-second timeout.");

            var contentionDelta = Monitor.LockContentionCount - contentionBefore;
            _output.WriteLine(
                $"[contention] delta={contentionDelta}  items={workItems}  " +
                $"per_item={contentionDelta / (double)workItems:F5}");

            // Documentation test for the current pool — records the number, no strict gate.
            // Phase 3 gate (dae34f6d): Monitor.Contention must be ≈0 on the rewritten pool.
        }

        // -----------------------------------------------------------------
        // 3. EventCounters/Meters stubs smoke test
        // -----------------------------------------------------------------

        [Fact(DisplayName = "PoolMetrics meter publishes expected instruments")]
        public void PoolMetrics_PublishesExpectedInstruments()
        {
            var found = new List<string>();

            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == PoolMetrics.MeterName)
                    found.Add(instrument.Name);
            };
            // Start enumerates already-published instruments; new ones fire via the callback.
            listener.Start();
            // Force static initialization if not yet triggered.
            _ = PoolMetrics.Meter;

            _output.WriteLine($"[metrics] instruments: {string.Join(", ", found)}");

            Assert.Contains("pool.work_items.queued", found);
            Assert.Contains("pool.workers.active", found);
            Assert.Contains("pool.workers.parked", found);
        }
    }
}
