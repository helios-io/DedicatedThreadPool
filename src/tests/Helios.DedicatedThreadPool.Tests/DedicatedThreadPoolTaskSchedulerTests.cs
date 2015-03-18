using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Helios.Concurrency.Tests
{
    [TestFixture]
    public class DedicatedThreadPoolTaskSchedulerTests
    {
        protected TaskScheduler Scheduler;
        protected TaskFactory Factory;
        private DedicatedThreadPool Pool;

        [SetUp]
        public void SetUp()
        {
            Pool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount));
            Scheduler = new DedicatedThreadPoolTaskScheduler(Pool);
            Factory = new TaskFactory(Scheduler);
        }

        [TearDown]
        public void TearDown()
        {
            Pool.Dispose();
        }

        [Test(Description = "Shouldn't immediately try to schedule all threads for task execution")]
        [Ignore("Totally unpredictable on low powered machines")]
        public void Should_only_use_one_thread_for_single_task_request()
        {
            var allThreadIds = new ConcurrentBag<int>();

            Pool.QueueUserWorkItem(() =>
            {
                allThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
            });

            Pool.QueueUserWorkItem(() =>
            {
                allThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
            });

            var task = Factory.StartNew(() =>
            {
                allThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
            });

            task.Wait();

            Assert.AreEqual(Pool.Settings.NumThreads, allThreadIds.Count);
        }

        [Test(Description = "Should be able to utilize the entire DedicatedThreadPool for queuing tasks")]
        public void Should_use_all_threads_for_many_tasks()
        {
            var threadIds = new ConcurrentBag<int>();
            var atomicCounter = new AtomicCounter(0);
            Action callback = () =>
            {
                atomicCounter.GetAndIncrement();
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
            };

            for (var i = 0; i < 1000; i++)
            {
                Factory.StartNew(callback);
            }
            //spin until work is completed
            SpinWait.SpinUntil(() => atomicCounter.Current == 1000, TimeSpan.FromSeconds(1));

            Assert.AreEqual(Pool.Settings.NumThreads, threadIds.Distinct().Count());
        }
    }
}
