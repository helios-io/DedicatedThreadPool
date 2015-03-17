using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Helios.Concurrency.Tests
{
    [TestFixture]
    public class DedicatedThreadPoolTests
    {
        [Test(Description = "Simple test to ensure that the entire thread pool doesn't just crater")]
        public void Should_process_multithreaded_workload()
        {
            var atomicCounter = new AtomicCounter(0);
            using (var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(2)))
            {
                for (var i = 0; i < 1000; i++)
                {
                    threadPool.QueueUserWorkItem(o => atomicCounter.GetAndIncrement());
                }
                SpinWait.SpinUntil(() => atomicCounter.Current == 1000, TimeSpan.FromSeconds(1));
            }
            Assert.Pass(string.Format("Passed! Final counter value: {0} / Expected {1}", atomicCounter.Current, 1000));
        }

        [Test(Description = "Ensure that the number of threads running in the pool concurrently equal exactly the DedicatedThreadPoolSettings.NumThreads property")]
        public void Should_process_workload_across_exactly_DedicatedThreadPoolSettings_NumThreads()
        {
            var numThreads = 3;
            var threadIds = new ConcurrentBag<int>();
            var atomicCounter = new AtomicCounter(0);
            WaitCallback callback = o =>
            {
                atomicCounter.GetAndIncrement();
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
            };
            using (var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(numThreads)))
            {
                for (var i = 0; i < numThreads; i++)
                {
                    threadPool.QueueUserWorkItem(callback);
                }
                //spin until work is completed
                SpinWait.SpinUntil(() => atomicCounter.Current == 1000, TimeSpan.FromSeconds(1));
            }

            Assert.AreEqual(numThreads, threadIds.Distinct().Count());
        }

        [Test(Description = "No sleeping threads - should release them in the event that there's no work.")]
        public void Should_release_threads_when_idle()
        {
            var numThreads = 3;
            var threadIds = new ConcurrentBag<int>();
            WaitCallback callback = o =>
            {
                Thread.Sleep(15); //sleep, so another thread is forced to take the work
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
            };
            using (var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(numThreads)))
            {
                for (var i = 0; i < numThreads; i++)
                {
                    threadPool.QueueUserWorkItem(callback);
                }
                //wait a second for all work to be completed
                Task.Delay(TimeSpan.FromSeconds(0.5)).Wait();

                //run the job again. Should get 3 more managed thread IDs
                for (var i = 0; i < numThreads; i++)
                {
                    threadPool.QueueUserWorkItem(callback);
                }
                Task.Delay(TimeSpan.FromSeconds(0.5)).Wait();
            }

            Assert.AreEqual(numThreads*2, threadIds.Distinct().Count());
        }

        [Test(Description = "Have a user-defined method that throws an exception? The world should not end.")]
        public void World_should_not_end_if_exception_thrown_in_user_callback()
        {
            var numThreads = 3;
            var threadIds = new ConcurrentBag<int>();
            WaitCallback badCallback = o =>
            {
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
                throw new Exception("DEATH TO THIS THREAD I SAY!");
            };
            WaitCallback goodCallback = o =>
            {
                Thread.Sleep(20);
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
            };

            using (var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(numThreads)))
            {
                for (var i = 0; i < numThreads; i++)
                {
                    threadPool.QueueUserWorkItem(badCallback);
                }
                //wait a second for all work to be completed
                Task.Delay(TimeSpan.FromSeconds(0.5)).Wait();

                //run the job again. Should get 3 more managed thread IDs
                for (var i = 0; i < numThreads; i++)
                {
                    threadPool.QueueUserWorkItem(goodCallback);
                }
                Task.Delay(TimeSpan.FromSeconds(0.5)).Wait();
            }

            // half of thread IDs should belong to failed threads, other half to successful ones
            Assert.AreEqual(numThreads * 2, threadIds.Distinct().Count());
        }
    }
}
