using System.Diagnostics.Metrics;

namespace Helios.Concurrency
{
    /// <summary>
    /// Phase 3–4 measurement stubs: the metrics wiring point for the rewritten pool.
    /// Instruments are defined here with stub implementations; Phase 3 will record real values.
    /// </summary>
    internal static class PoolMetrics
    {
        internal const string MeterName = "Helios.DedicatedThreadPool";

        internal static readonly Meter Meter = new(MeterName, "0.3.0");

        // Work item throughput — Phase 3 will increment on each enqueue.
        internal static readonly Counter<long> WorkItemsQueued =
            Meter.CreateCounter<long>(
                "pool.work_items.queued",
                description: "Total work items enqueued.");

        // Active worker count — Phase 3 will add/subtract around each worker loop iteration.
        internal static readonly UpDownCounter<int> ActiveWorkers =
            Meter.CreateUpDownCounter<int>(
                "pool.workers.active",
                description: "Worker threads currently executing items.");

        // Parked worker count — stub returns 0 until Phase 3 wires the LIFO blocker stack.
        internal static readonly ObservableGauge<int> ParkedWorkers =
            Meter.CreateObservableGauge<int>(
                "pool.workers.parked",
                () => 0,
                description: "Worker threads currently parked (waiting for work).");
    }
}
