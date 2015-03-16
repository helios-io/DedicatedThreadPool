namespace Helios.DedicatedThreadPool
{
    /// <summary>
    /// An instanced, dedicated thread pool.
    /// </summary>
    public class HeliosThreadPool
    {
        public HeliosThreadPool(HeliosThreadPoolSettings settings)
        {
            Settings = settings;
        }

        public HeliosThreadPoolSettings Settings { get; private set; }

        public int ThreadCount { get { return Settings.NumThreads; } }

        public ThreadType ThreadType { get { return Settings.ThreadType; } }
    }
}
