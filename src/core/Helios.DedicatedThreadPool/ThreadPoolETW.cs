using System.Diagnostics.Tracing;
using System.Threading;

namespace Helios.Concurrency
{
    internal sealed class DedicatedThreadPoolSource : EventSource
    {
        /// <summary>
        /// Borrowed from the main Helios library in order to make it easier
        /// to run unit tests on top of the 
        /// </summary>
        internal class AtomicCounter
        {
            public AtomicCounter(int seed)
            {
                _seed = seed;
            }

            private int _seed;

            /// <summary>
            /// Retrieves the current value of the counter
            /// </summary>
            public int Current
            {
                get { return _seed; }
            }

            /// <summary>
            /// Increments the counter and returns the next value
            /// </summary>
            public int Next
            {
                get { return Interlocked.Increment(ref _seed); }
            }

            /// <summary>
            /// Returns the current value while simultaneously incrementing the counter
            /// </summary>
            public int GetAndIncrement()
            {
                var rValue = Current;
                var nextValue = Next;
                return rValue;
            }
        }

        internal static class DebugCounters
        {
            public const string StealName = "WorkerQueueSteal";
            public static readonly AtomicCounter StealCounter = new AtomicCounter(0);

            public const string StealMissName = "WorkerQueueStealMiss";
            public static readonly AtomicCounter StealMissCounter = new AtomicCounter(0);

            public const string HitName = "WorkerGlobalQueueHit";
            public static readonly AtomicCounter HitCounter = new AtomicCounter(0);

            public const string MissName = "WorkerGlobalQueueMiss";
            public static readonly AtomicCounter MissCounter = new AtomicCounter(0);
        }

        public void Load(long imageBase, string name)
        {
            WriteEvent(1, imageBase, name);
        }

        public void Message(string message)
        {
            WriteEvent(5, message);
        }

        public void HighFreq(string name, int value = 0)
        {
            if (IsEnabled())
            {
                WriteEvent(7, name, value);
            }
        }

        public void StealHit()
        {
            HighFreq(DebugCounters.StealName, DebugCounters.StealCounter.GetAndIncrement());
        }

        public void StealMiss()
        {
            HighFreq(DebugCounters.StealMissName, DebugCounters.StealMissCounter.GetAndIncrement());
        }

        public void GlobalQueueHit()
        {
            HighFreq(DebugCounters.HitName, DebugCounters.HitCounter.GetAndIncrement());
        }

        public void GlobalQueueMiss()
        {
            HighFreq(DebugCounters.MissName, DebugCounters.MissCounter.GetAndIncrement());
        }

        public static readonly DedicatedThreadPoolSource Log = new DedicatedThreadPoolSource();
    }
}
