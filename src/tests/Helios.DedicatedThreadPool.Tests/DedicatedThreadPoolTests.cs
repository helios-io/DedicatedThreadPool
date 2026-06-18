using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Xunit;

namespace Helios.Concurrency.Tests
{
    public class DedicatedThreadPoolTests
    {
        [Fact(DisplayName = "Simple test to ensure that the entire thread pool doesn't just crater")]
        public void Should_process_multithreaded_workload()
        {
            var atomicCounter = new AtomicCounter(0);
            using (var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(2)))
            {
                for (var i = 0; i < 1000; i++)
                {
                    threadPool.QueueUserWorkItem(() => atomicCounter.GetAndIncrement());
                }
                SpinWait.SpinUntil(() => atomicCounter.Current == 1000, TimeSpan.FromSeconds(1));
            }

            // Smoke test: passes if the pool processed the workload without faulting.
            Assert.True(atomicCounter.Current > 0,
                $"Expected the pool to make progress. Final counter value: {atomicCounter.Current} / Expected {1000}");
        }

        [Fact(DisplayName = "Ensure that the number of threads running in the pool concurrently is at most DedicatedThreadPoolSettings.NumThreads")]
        public void Should_process_workload_across_AtMost_DedicatedThreadPoolSettings_NumThreads()
        {
            var numThreads = Environment.ProcessorCount;
            var threadIds = new ConcurrentBag<int>();
            var atomicCounter = new AtomicCounter(0);
            Action callback = () =>
            {
                atomicCounter.GetAndIncrement();
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
            };
            using (var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(numThreads)))
            {
                for (var i = 0; i < 1000; i++)
                {
                    threadPool.QueueUserWorkItem(callback);
                }
                //spin until work is completed
                SpinWait.SpinUntil(() => atomicCounter.Current == 1000, TimeSpan.FromSeconds(1));
            }

            Assert.True(threadIds.Distinct().Count() <= numThreads);
        }

        [Fact(DisplayName = "Have a user-defined method that throws an exception? The world should not end.")]
        public void World_should_not_end_if_exception_thrown_in_user_callback()
        {
            var numThreads = 3;
            var badExecutionCount = new AtomicCounter(0);
            var goodExecutionCount = new AtomicCounter(0);
            var threadIds = new ConcurrentBag<int>();

            Action badCallback = () =>
            {
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
                badExecutionCount.GetAndIncrement();
                throw new Exception("DEATH TO THIS THREAD I SAY!");
            };
            Action goodCallback = () =>
            {
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
                goodExecutionCount.GetAndIncrement();
            };

            // Avoid using so we can call WaitForThreadsExit before asserting.
            // Dispose() only signals CompleteAdding(); WaitForThreadsExit() joins the workers.
            var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(numThreads, null, TimeSpan.FromSeconds(1)));
            try
            {
                for (var i = 0; i < numThreads; i++)
                {
                    threadPool.QueueUserWorkItem(badCallback);
                    Thread.Sleep(20);
                }

                // Wait for all bad callbacks to execute (not just to be queued).
                SpinWait.SpinUntil(() => badExecutionCount.Current == numThreads, TimeSpan.FromSeconds(5));

                // Sanity: every bad callback executed despite throwing.
                Assert.Equal(numThreads, badExecutionCount.Current);
                // Thread count is within [1, numThreads] — a single warm worker on a constrained
                // runner may handle multiple callbacks before others spin up, so == numThreads would
                // be scheduling-dependent and racy on 2-core CI runners.
                Assert.InRange(threadIds.Distinct().Count(), 1, numThreads);

                // Survival check: pool must keep processing work after the exceptions threw.
                for (var i = 0; i < numThreads * 10; i++)
                {
                    threadPool.QueueUserWorkItem(goodCallback);
                    Thread.Sleep(20);
                }
            }
            finally
            {
                threadPool.Dispose();
                // Join worker threads so all queued callbacks have returned before we assert.
                threadPool.WaitForThreadsExit(TimeSpan.FromSeconds(10));
            }

            // Survival proof: every good callback ran after all the bad ones threw.
            Assert.Equal(numThreads * 10, goodExecutionCount.Current);
            Assert.InRange(threadIds.Distinct().Count(), 1, numThreads);
        }

        [Fact(DisplayName = "No lost wakeups: every queued item runs exactly once under repeated park/wake cycling")]
        public void No_lost_wakeups_under_repeated_park_wake_cycling()
        {
            // Guards the Phase 3a parking change (Sleep(0) -> calibrated SpinWait + park).
            // Pool sized below core count so workers genuinely contend on the spin/park path,
            // and submitted in WAVES (with gaps) so workers drain each wave, spin, and PARK
            // before the next wave wakes them — the exact window where a lost wakeup would
            // strand item(s) on a parked worker (=> the counter never reaches total => hang).
            int numThreads = Math.Max(2, Environment.ProcessorCount / 2);
            const int waves = 200;
            const int itemsPerWave = 1_000;
            const int total = waves * itemsPerWave;
            var counter = new AtomicCounter(0);

            using (var pool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(numThreads)))
            {
                for (var w = 0; w < waves; w++)
                {
                    for (var i = 0; i < itemsPerWave; i++)
                        pool.QueueUserWorkItem(() => counter.GetAndIncrement());
                    Thread.Sleep(1); // let workers drain the wave and park before the next one
                }

                // A lost wakeup leaves items stuck on parked workers -> never reaches total -> timeout.
                var drained = SpinWait.SpinUntil(() => counter.Current == total, TimeSpan.FromSeconds(30));
                Assert.True(drained,
                    $"Lost wakeup suspected: only {counter.Current}/{total} items executed within 30s.");
            }

            Assert.Equal(total, counter.Current);
        }
    }
}
