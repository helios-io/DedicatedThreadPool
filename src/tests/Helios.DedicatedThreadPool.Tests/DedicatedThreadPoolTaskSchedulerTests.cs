﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Helios.Concurrency.Tests
{
    public class DedicatedThreadPoolTaskSchedulerTests : IDisposable
    {
        protected TaskScheduler Scheduler;
        protected TaskFactory Factory;
        private DedicatedThreadPool Pool;

        public DedicatedThreadPoolTaskSchedulerTests()
        {
            Pool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount));
            Scheduler = new DedicatedThreadPoolTaskScheduler(Pool);
            Factory = new TaskFactory(Scheduler);
        }

        // "Shouldn't immediately try to schedule all threads for task execution"
        [Fact(Skip = "Totally unpredictable on low powered machines")]
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

            Assert.Equal(Pool.Settings.NumThreads, allThreadIds.Count);
        }

        // "Should be able to utilize the entire DedicatedThreadPool for queuing tasks"
        [Fact]
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

            Assert.True(threadIds.Distinct().Count() <= Pool.Settings.NumThreads);
        }

        public void Dispose()
        {
            Pool?.Dispose();
        }
    }
}
