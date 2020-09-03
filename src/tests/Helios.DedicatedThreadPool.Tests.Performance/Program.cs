using NBench;

namespace Helios.Concurrency.Tests.Performance
{
    class Program
    {
        static int Main(string[] args)
        {
            return NBenchRunner.Run<Program>();
        }
    }
}
