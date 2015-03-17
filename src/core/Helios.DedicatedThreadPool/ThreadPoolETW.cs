using System;
using System.Threading;
using Microsoft.Diagnostics.Tracing;

namespace Helios.Concurrency
{
    [EventSource(Name = "DedicatedThreadPool")]
    internal sealed class DedicatedThreadPoolSource : EventSource
    {
        public static string AssemblyDirectory
        {
            get
            {
                var codeBase = typeof(DedicatedThreadPoolSource).Assembly.CodeBase;
                var uri = new UriBuilder(codeBase);
                var path = Uri.UnescapeDataString(uri.Path);
                return path;
            }
        }

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
            public static readonly AtomicCounter StealCounter = new AtomicCounter(0);
            public static readonly AtomicCounter StealMissCounter = new AtomicCounter(0);
            public static readonly AtomicCounter GlobalHitCounter = new AtomicCounter(0);
            public static readonly AtomicCounter GlobalMissCounter = new AtomicCounter(0);
            public static readonly AtomicCounter LocalHitCounter = new AtomicCounter(0);
            public static readonly AtomicCounter LocalMissCounter = new AtomicCounter(0);
        }

        public void Message(string message)
        {
            WriteEvent(1, message);
        }

        public void StealHit()
        {
            WriteEvent(2, DebugCounters.StealCounter.GetAndIncrement());
        }

        public void StealMiss()
        {
            WriteEvent(3, DebugCounters.StealMissCounter.GetAndIncrement());
        }

        public void GlobalQueueHit()
        {
            WriteEvent(4, DebugCounters.GlobalHitCounter.GetAndIncrement());
        }

        public void GlobalQueueMiss()
        {
            WriteEvent(5, DebugCounters.GlobalMissCounter.GetAndIncrement());
        }

        public void LocalQueueHit()
        {
            WriteEvent(6, DebugCounters.LocalHitCounter.GetAndIncrement());
        }

        public void LocalQueueMiss()
        {
            WriteEvent(7, DebugCounters.LocalMissCounter.GetAndIncrement());
        }

        public void ThreadStarted()
        {
            WriteEvent(8);
        }

        public static readonly DedicatedThreadPoolSource Log = new DedicatedThreadPoolSource();
    }
}
