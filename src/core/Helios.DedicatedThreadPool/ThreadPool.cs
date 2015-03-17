using System;
using System.Diagnostics.Contracts;
using System.Security;
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
            WorkQueue = new ThreadPoolWorkQueue();
        }

        public DedicatedThreadPoolSettings Settings { get; private set; }

        public int ThreadCount { get { return Settings.NumThreads; } }

        public ThreadType ThreadType { get { return Settings.ThreadType; } }

        /// <summary>
        /// The global work queue, shared by all threads.
        /// 
        /// Each local thread has its own work-stealing local queue.
        /// </summary>
        public ThreadPoolWorkQueue WorkQueue { get; private set; }

        public bool WasDisposed { get; private set; }

        private volatile bool _shutdownRequested;

        private void Shutdown()
        {
            _shutdownRequested = true;
        }

        private volatile int numOutstandingThreadRequests = 0;

        public bool QueueUserWorkItem(WaitCallback work)
        {
            return QueueUserWorkItem(work, null);
        }

        public bool QueueUserWorkItem(WaitCallback work, object obj)
        {
            bool success = true;
            if (work != null)
            {
                //
                // If we are able to create the workitem, we need to get it in the queue without being interrupted
                // by a ThreadAbortException.
                //
                try
                {
                }
                finally
                {
                    var heliosActionCallback = new ActionWorkItem(work, obj);
                    WorkQueue.Enqueue(heliosActionCallback, true);
                    EnsureThreadRequested();
                    success = true;
                }
            }
            else
            {
                throw new ArgumentNullException("callback");
            }
            return success;
        }

        /// <summary>
        /// Method run internally by each worker thread
        /// </summary>
        private bool Dispatch()
        {
            var workQueue = WorkQueue;

            //
            // The clock is ticking!  We have ThreadPoolGlobals.tpQuantum milliseconds to get some work done, and then
            // we need to return to the VM.
            //
            int quantumStartTime = Environment.TickCount;

            //
            // Update our records to indicate that an outstanding request for a thread has now been fulfilled.
            // From this point on, we are responsible for requesting another thread if we stop working for any
            // reason, and we believe there might still be work in the queue.
            MarkThreadRequestSatisfied();

            bool needAnotherThread = true;
            IHeliosWorkItem workItem = null;
            try
            {
                //Set up thread-local data
                ThreadPoolWorkQueueThreadLocals tl = workQueue.EnsureCurrentThreadHasQueue();
                while ((Environment.TickCount - quantumStartTime) < Settings.QuantumMillis) //look for work until explicitly shut down or too many queue misses
                {
                    bool missedSteal = false;
                    workQueue.Dequeue(tl, out workItem, out missedSteal);

                    try
                    {
                    }
                    finally
                    {
                        if (workItem == null)
                        {
                            //
                            // No work.  We're going to return to the VM once we leave this protected region.
                            // If we missed a steal, though, there may be more work in the queue.
                            // Instead of looping around and trying again, we'll just request another thread.  This way
                            // we won't starve other AppDomains while we spin trying to get locks, and hopefully the thread
                            // that owns the contended work-stealing queue will pick up its own workitems in the meantime, 
                            // which will be more efficient than this thread doing it anyway.
                            //
                            needAnotherThread = missedSteal;
                        }
                        else
                        {
                            //
                            // If we found work, there may be more work.  Ask for another thread so that the other work can be processed
                            // in parallel.  Note that this will only ask for a max of #procs threads, so it's safe to call it for every dequeue.
                            //
                            EnsureThreadRequested();
                        }
                    }

                    if (workItem == null)
                    {
                        return true;
                    }
                    else //execute our work
                    {
                        workItem.ExecuteWorkItem();
                        workItem = null;
                    }
                }
                return true;
            }
            finally
            {
                //had an exception in the course of executing some work, and this thread is going to die.
                if (needAnotherThread)
                    EnsureThreadRequested();
            }

            //should never hit this code, unless something catastrophically bad happened (like an aborted thread)
            Contract.Assert(false);
            return true;
        }

        internal void RequestWorkerThread()
        {
            //don't acknowledge thread create requests when disposing or stopping
            if (!_shutdownRequested)
            {
                var thread = new Thread(_ => Dispatch()) { IsBackground = ThreadType == ThreadType.Background };
                thread.Start();
            }
        }

        [SecurityCritical]
        internal void EnsureThreadRequested()
        {
            //
            // If we have not yet requested #procs threads from the VM, then request a new thread.
            // Note that there is a separate count in the VM which will also be incremented in this case, 
            // which is handled by RequestWorkerThread.
            //
            int count = numOutstandingThreadRequests;
            while (count < ThreadCount)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count + 1, count);
                if (prev == count)
                {
                    RequestWorkerThread();
                    break;
                }
                count = prev;
            }
        }

        [SecurityCritical]
        internal void MarkThreadRequestSatisfied()
        {

#if HELIOS_DEBUG
            DedicatedThreadPoolSource.Log.ThreadStarted();
#endif
            //
            // The VM has called us, so one of our outstanding thread requests has been satisfied.
            // Decrement the count so that future calls to EnsureThreadRequested will succeed.
            // Note that there is a separate count in the VM which has already been decremented by the VM
            // by the time we reach this point.
            //
            int count = numOutstandingThreadRequests;
            while (count > 0)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count - 1, count);
                if (prev == count)
                {
                    break;
                }
                count = prev;
            }
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
    }
}
