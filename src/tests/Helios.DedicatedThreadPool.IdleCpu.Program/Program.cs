using System;
using System.Threading.Tasks;
using Helios.Concurrency;

namespace Helios.DedicatedThreadPool.IdleCpu.Program
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var maxThreads = Math.Max(4, Environment.ProcessorCount);
            
            Console.WriteLine("Starting Idle Cpu Test with Following Configuration");
            Console.WriteLine("MaxThreads: {0}", maxThreads);

            var settings = new DedicatedThreadPoolSettings(maxThreads);
            var threadPool = new Concurrency.DedicatedThreadPool(settings);

            await threadPool.WaitForThreadsExit();
        }
    }
}
