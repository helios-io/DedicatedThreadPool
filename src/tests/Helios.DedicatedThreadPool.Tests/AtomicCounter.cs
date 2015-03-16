using System.Threading;

namespace Helios.Concurrency.Tests
{
    /// <summary>
    /// Borrowed from the main Helios library in order to make it easier
    /// to run unit tests on top of the 
    /// </summary>
    public class AtomicCounter
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
}
