using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Helios.Concurrency;

namespace Helios.DedicatedThreadPool.VsThreadpoolBenchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var generations = 4;
            var tpSettings = new DedicatedThreadPoolSettings(Environment.ProcessorCount);
            for (int i = 0; i < generations; i++)
            {
                var workItems = 10000 * (int)Math.Pow(10, i);
                Console.WriteLine(
                    "Comparing Helios.Concurrency.DedicatedThreadPool vs Helios.Concurrency.DedicatedThreadFiber for {0} items",
                    workItems);
                Console.WriteLine("DedicatedThreadFiber.NumThreads: {0}", tpSettings.NumThreads);

                Console.WriteLine("System.Threading.ThreadPool");
                Console.WriteLine(
                    TimeSpan.FromMilliseconds(
                        Enumerable.Range(0, 6).Select(_ =>
                        {
                            var sw = Stopwatch.StartNew();
                            CreateAndWaitForWorkItems(workItems);
                            return sw.ElapsedMilliseconds;
                        }).Skip(1).Average()
                        )
                    );

                Console.WriteLine("Helios.Concurrency.DedicatedThreadFiber");
                Console.WriteLine(
                    TimeSpan.FromMilliseconds(
                        Enumerable.Range(0, 6).Select(_ =>
                        {
                            var sw = Stopwatch.StartNew();
                            CreateAndWaitForWorkItems(workItems, tpSettings);
                            return sw.ElapsedMilliseconds;
                        }).Skip(1).Average()
                        )
                    );
            }
        }

        static void CreateAndWaitForWorkItems(int numWorkItems)
        {
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                int itemsRemaining = numWorkItems;
                for (int i = 0; i < numWorkItems; i++)
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        if (Interlocked.Decrement(
                            ref itemsRemaining) == 0) mre.Set();
                    });
                }
                mre.WaitOne();
            }
        }

        static void CreateAndWaitForWorkItems(int numWorkItems, DedicatedThreadPoolSettings settings)
        {
            using (ManualResetEvent mre = new ManualResetEvent(false))
            using(var tp = new Concurrency.DedicatedThreadPool(settings))
            {
                int itemsRemaining = numWorkItems;
                for (int i = 0; i < numWorkItems; i++)
                {
                    tp.QueueUserWorkItem(delegate
                    {
                        if (Interlocked.Decrement(
                            ref itemsRemaining) == 0) mre.Set();
                    });
                }
                mre.WaitOne();
            }
        }
    }
}
