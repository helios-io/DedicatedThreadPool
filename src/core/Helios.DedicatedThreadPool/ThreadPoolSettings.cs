using System;

namespace Helios.Concurrency
{
    /// <summary>
    /// The type of threads to use - either foreground or background threads.
    /// </summary>
    public enum ThreadType
    {
        Foreground,
        Background
    }

    /// <summary>
    /// Provides settings for a dedicated thread pool
    /// </summary>
    internal class DedicatedThreadPoolSettings
    {
        /// <summary>
        /// The default number of milliseconds we'll spin to check for work again
        /// on any of the thread queues
        /// </summary>
        public const int DefaultThreadSpinWaitMilis = 30;

        /// <summary>
        /// Background threads are the default thread type
        /// </summary>
        public const ThreadType DefaultThreadType = ThreadType.Background;

        public DedicatedThreadPoolSettings(int numThreads) : this(numThreads, DefaultThreadType) { }

        public DedicatedThreadPoolSettings(int numThreads, ThreadType threadType) : this(numThreads, threadType, DefaultThreadSpinWaitMilis) { }

        public DedicatedThreadPoolSettings(int numThreads, ThreadType threadType, int threadWaitForWorkMillis)
        {
            ThreadWaitForWorkMillis = threadWaitForWorkMillis;
            ThreadType = threadType;
            NumThreads = numThreads;
            if(numThreads <= 0) 
                throw new ArgumentOutOfRangeException("numThreads", string.Format("numThreads must be at least 1. Was {0}", numThreads));
            if (ThreadWaitForWorkMillis <= 0)
                throw new ArgumentOutOfRangeException("threadWaitForWorkMillis", string.Format("threadSpinWaitMillis must be at least 1. Was {0}", numThreads));
        }

        /// <summary>
        /// The total number of threads to run in this thread pool.
        /// </summary>
        public int NumThreads { get; private set; }

        /// <summary>
        /// The type of threads to run in this thread pool.
        /// </summary>
        public ThreadType ThreadType { get; private set; }

        /// <summary>
        /// The number of milliseconds each dedicated thread will spin while
        /// waiting for more work. If there's work in the queue, the threads don't spin.
        /// </summary>
        public int ThreadWaitForWorkMillis { get; private set; }
    }
}
