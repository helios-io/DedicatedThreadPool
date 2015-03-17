using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;

namespace Helios.Concurrency
{
    /// <summary>
    /// An instanced, dedicated thread pool.
    /// </summary>
    internal class DedicatedThreadPool : IDisposable
    {
        public DedicatedThreadPool(DedicatedThreadPoolSettings settings)
        {
            Settings = settings;
            Workers = Enumerable.Repeat(new WorkerQueue(), settings.NumThreads).ToArray();
            foreach (var worker in Workers)
            {
                new PoolWorker(worker, this);
            }
        }

        public DedicatedThreadPoolSettings Settings { get; private set; }        

        internal volatile bool ShutdownRequested;

        public readonly WorkerQueue[] Workers;

        [ThreadStatic]
        public static PoolWorker CurrentWorker;

        /// <summary>
        /// index for round-robin load-balancing across worker threads
        /// </summary>
        private volatile int _index = 0;

        public bool WasDisposed { get; private set; }

        private void Shutdown()
        {
            ShutdownRequested = true;
        }

        private void RequestThread(WorkerQueue unclaimedQueue)
        {
            var worker = new PoolWorker(unclaimedQueue, this);
        }

        public bool QueueUserWorkItem(Action work)
        {
            bool success = true;
            if (work != null)
            {
                //no local queue, write to a round-robin queue
                //if (null == CurrentWorker)
                //{
                    //using volatile instead of interlocked, no need to be exact, gaining 20% perf
                    unchecked
                    {
                        _index = (_index + 1);
                        Workers[_index & 0x7fffffff  % Settings.NumThreads].AddWork(work);
                    }
                //}
                //else //recursive task queue, write directly
                //{
                //    // send work directly to PoolWorker
                //    // CurrentWorker.AddWork(work);
                //}
            }
            else
            {
                throw new ArgumentNullException("work");
            }
            return success;
        }

        #region IDisposable members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool isDisposing)
        {
            if (!WasDisposed)
            {
                if (isDisposing)
                {
                    Shutdown();
                }
            }

            WasDisposed = true;
        }

        #endregion

        #region Pool worker implementation

        internal sealed class WorkerQueue
        {
            internal ConcurrentQueue<Action> WorkQueue = new ConcurrentQueue<Action>();
            internal readonly ManualResetEventSlim Event = new ManualResetEventSlim(false);

            public void AddWork(Action work)
            {
                WorkQueue.Enqueue(work);
                Event.Set();
            }
        }

        public class PoolWorker
        {
            private WorkerQueue _work;
            private DedicatedThreadPool _pool;

            private ManualResetEventSlim _event;
            private ConcurrentQueue<Action> _workQueue;

            public PoolWorker(WorkerQueue work, DedicatedThreadPool pool)
            {
                _work = work;
                _pool = pool;
                _event = _work.Event;
                _workQueue = _work.WorkQueue;

                var thread = new Thread(() =>
                {
                    CurrentWorker = this;
                    
                    
                    while (!_pool.ShutdownRequested)
                    {
                        //suspend if no more work is present
                        _event.Wait();
                        
                        Action action;
                        while (_workQueue.TryDequeue(out action))
                        {
                            try
                            {
                                action();
                            }
                            catch (Exception ex)
                            {
                                /* request a new thread then shut down */
                                _pool.RequestThread(_work);
                                CurrentWorker = null;
                                _work = null;
                                _event = null;
                                _workQueue = null;
                                _pool = null;
                                throw;
                            }
                        }
                        if (_workQueue.Count == 0)
                        {
                            _event.Reset();
                        }
                        if (_workQueue.Count > 0)
                        {
                            _event.Set();
                        }
                    }
                })
                {
                    IsBackground = _pool.Settings.ThreadType == ThreadType.Background
                };
                thread.Start();
            }
        }

        #endregion
    }
}
