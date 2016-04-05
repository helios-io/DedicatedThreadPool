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
                    threadPool.QueueUserWorkItem(() => atomicCounter.GetAndIncrement());
                }
                SpinWait.SpinUntil(() => atomicCounter.Current == 1000, TimeSpan.FromSeconds(1));
            }
            Assert.Pass(string.Format("Passed! Final counter value: {0} / Expected {1}", atomicCounter.Current, 1000));
        }

        [Test(Description = "Ensure that the number of threads running in the pool concurrently equal is AtMost equal to the DedicatedThreadPoolSettings.NumThreads property")]
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

        [Test(Description = "Have a user-defined method that throws an exception? The world should not end.")]
        public void World_should_not_end_if_exception_thrown_in_user_callback()
        {
            var numThreads = 3;
            var threadIds = new ConcurrentBag<int>();
            Action badCallback = () =>
            {
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
                throw new Exception("DEATH TO THIS THREAD I SAY!");
            };
            Action goodCallback = () =>
            {
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
            };

            using (var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(numThreads, null, TimeSpan.FromSeconds(1))))
            {
                for (var i = 0; i < numThreads; i++)
                {
                    threadPool.QueueUserWorkItem(badCallback);
                    Thread.Sleep(20);
                }

                //sanity check
                Assert.AreEqual(numThreads, threadIds.Distinct().Count());

                //run the job again. Should get the same thread IDs as before
                for (var i = 0; i < numThreads*10; i++)
                {
                    threadPool.QueueUserWorkItem(goodCallback);
                    Thread.Sleep(20);
                }
            }

            // half of thread IDs should belong to failed threads, other half to successful ones
            Assert.AreEqual(numThreads, threadIds.Distinct().Count());
        }
    }
}
