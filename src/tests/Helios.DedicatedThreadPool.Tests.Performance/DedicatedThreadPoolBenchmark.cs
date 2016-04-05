using System;
using System.Threading;
using NBench;

namespace Helios.Concurrency.Tests.Performance
{
    public class DedicatedThreadPoolBenchmark
    {
        private const string BenchmarkCounterName = "BenchmarkCalls";
        private const int ThreadCalls = 100000; //100K
        private const double MinExpectedThroughput = 1000000.0d;
        private Counter _counter;
        private DedicatedThreadPool _threadPool;
        private DedicatedThreadPoolSettings _settings;

        [PerfSetup]
        public void Setup(BenchmarkContext context)
        {
            _counter = context.GetCounter(BenchmarkCounterName);
            _settings = new DedicatedThreadPoolSettings(Environment.ProcessorCount);
            _threadPool = new DedicatedThreadPool(_settings);
        }

        [PerfBenchmark(RunMode = RunMode.Iterations, RunTimeMilliseconds = 1000, NumberOfIterations = 13)]
        [CounterThroughputAssertion(BenchmarkCounterName, MustBe.GreaterThan, MinExpectedThroughput)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        public void HeliosBenchmark(BenchmarkContext context)
        {
            CreateAndWaitForWorkItems(ThreadCalls, _settings);
        }

        [PerfBenchmark(RunMode = RunMode.Iterations, RunTimeMilliseconds = 1000, NumberOfIterations = 13)]
        [CounterThroughputAssertion(BenchmarkCounterName, MustBe.GreaterThan, MinExpectedThroughput)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        public void ThreadpoolBenchmark(BenchmarkContext context)
        {
            CreateAndWaitForWorkItems(ThreadCalls);
        }

        [PerfCleanup]
        public void Cleanup()
        {
            _threadPool.Dispose();
            _settings = null;
        }

        void CreateAndWaitForWorkItems(int numWorkItems)
        {
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                int itemsRemaining = numWorkItems;
                for (int i = 0; i < numWorkItems; i++)
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        _counter.Increment();
                        if (Interlocked.Decrement(
                            ref itemsRemaining) == 0)
                            mre.Set();
                    });
                }
                mre.WaitOne();
            }
        }

        void CreateAndWaitForWorkItems(int numWorkItems, DedicatedThreadPoolSettings settings)
        {
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                int itemsRemaining = numWorkItems;
                for (int i = 0; i < numWorkItems; i++)
                {
                    _threadPool.QueueUserWorkItem(delegate
                    {
                        _counter.Increment();
                        if (Interlocked.Decrement(
                            ref itemsRemaining) == 0)
                            mre.Set();
                    });
                }
                mre.WaitOne();
            }
        }
    }
}
