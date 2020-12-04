using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helios.Concurrency;

namespace Helios.DedicatedThreadPool.IdleCpu.Program
{
    /// <summary>
    /// Create random numbers with Thread-specific seeds.
    /// 
    /// Borrowed form Jon Skeet's brilliant C# in Depth: http://csharpindepth.com/Articles/Chapter12/Random.aspx
    /// </summary>
    public static class ThreadLocalRandom
    {
        private static int _seed = Environment.TickCount;

        private static ThreadLocal<Random> _rng = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref _seed)));

        /// <summary>
        /// The current random number seed available to this thread
        /// </summary>
        public static Random Current
        {
            get
            {
                return _rng.Value;
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            // set a huge number of threads per core to exaggerate idle CPU effects when we pre-allocate
            var maxThreads = Math.Max(4, Environment.ProcessorCount)*100;
            
            Console.WriteLine("Starting Idle Cpu Test with Following Configuration");
            Console.WriteLine("MaxThreads: {0}", maxThreads);

            var settings = new DedicatedThreadPoolSettings(maxThreads);
            var threadPool = new Concurrency.DedicatedThreadPool(settings);
            var concurrentBag = new ConcurrentBag<int>();

            void DoWork(int count)
            {
                Console.WriteLine("Count: {0}", count);
                Thread.Sleep(100);
                concurrentBag.Add(Thread.CurrentThread.ManagedThreadId);
                if (count % 3 == 0)
                    threadPool.QueueUserWorkItem(() => DoWork(ThreadLocalRandom.Current.Next(0,7)));
            }

            

            foreach (var i in Enumerable.Range(0, maxThreads*100))
            {
                threadPool.QueueUserWorkItem(() =>
                {
                    DoWork(i);
                });
            }

            threadPool.QueueUserWorkItem(() =>
            {
                Console.WriteLine("Found {0} active threads", concurrentBag.ToArray().Distinct().Count());
            });

            // force background Helios threads to run
            await Task.Delay(TimeSpan.FromMinutes(3));

            Console.WriteLine("Exited with {0} active threads", concurrentBag.ToArray().Distinct().Count());
        }
    }
}
