#### 0.2.0 Mar 26 2015
Added deadlock detection to the `DedicatedThreadPool` and other bug / performance fixes.

Deadlock detection is somewhat simplistic and it can't tell the difference between extremely long running tasks and a deadlock, so by default it's disabled.

However, if you want to enable it you can turn it on via the the `DedicatedThreadPoolSettings` class:

```csharp
/*
 * Any task running longer than 5 seconds will be assumed deadlocked
 * and aborted.
 */
using (var threadPool = new Helios.Concurrency.DedicatedThreadPool(
        new DedicatedThreadPoolSettings(numThreads, TimeSpan.FromSeconds(5))))
{
    threadPool.QueueUserWorkItem(() => { ... }));
}
```

#### 0.1.0 Mar 17 2015
Initial build of `DedicatedThreadPool`. Works via the following API:

```csharp
using (var threadPool = new Helios.Concurrency.DedicatedThreadPool(
        new DedicatedThreadPoolSettings(numThreads)))
{
    threadPool.QueueUserWorkItem(() => { ... }));
}
```

Creates a `DedicatedThreadPool` object which allocates a fixed number of threads, each with their own independent task queue.

This `DedicatedThreadPool` can also be used in combination with a `DedicatedThreadPoolTaskScheduler` for TPL support, like this:

```csharp
//use 3 threads
var Pool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(3));
var Scheduler = new DedicatedThreadPoolTaskScheduler(Pool);
var Factory = new TaskFactory(Scheduler);

 var task = Factory.StartNew(() =>
{
    //work that'll run on the dedicated thread pool...
});

task.Wait();
```