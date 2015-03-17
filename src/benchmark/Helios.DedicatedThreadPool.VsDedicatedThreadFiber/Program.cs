using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helios.Concurrency;

namespace Helios.DedicatedThreadPool.VsDedicatedThreadFiber
{
    class Program
    {
        static void Main(string[] args)
        {
            var workItems = 10000;
            var tpSettings = new DedicatedThreadPoolSettings(Environment.ProcessorCount);
            Console.WriteLine("Comparing Helios.Concurrency.DedicatedThreadPool vs Helios.Concurrency.DedicatedThreadFiber for {0} items", workItems);
            Console.WriteLine("DedicatedThreadPool.NumThreads: {0}", tpSettings.NumThreads);
            Console.WriteLine("DedicatedThreadFiber.NumThreads: {0}", tpSettings.NumThreads);

            Console.WriteLine("Helios.Concurrency.DedicatedThreadFiber");
            Console.WriteLine(
                TimeSpan.FromMilliseconds(
                    Enumerable.Range(0, 6).Select(_ =>
                    {
                        var sw = Stopwatch.StartNew();
                        CreateAndWaitForWorkItems(workItems,tpSettings.NumThreads);
                        return sw.ElapsedMilliseconds;
                    }).Skip(1).Average()
                )
            );

            Console.WriteLine("Helios.Concurrency.DedicatedThreadPool");
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

        static void CreateAndWaitForWorkItems(int numWorkItems, int numThreads)
        {
            using (ManualResetEvent mre = new ManualResetEvent(false))
            using(var fiber = FiberFactory.CreateFiber(numThreads))
            {
                int itemsRemaining = numWorkItems;
                for (int i = 0; i < numWorkItems; i++)
                {
                    fiber.Add(delegate
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
            using (var tp = new Concurrency.DedicatedThreadPool(settings))
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
