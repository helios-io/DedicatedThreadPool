# DedicatedThreadPool
An instanced, dedicated thread pool for eliminating "noisy neighbor" problems on the CLR `ThreadPool`.

## API

You can create a `Helios.Concurrency.DedicatedThreadPool` instance via the following API:

```csharp
using (var threadPool = new Helios.Concurrency.DedicatedThreadPool(
        new DedicatedThreadPoolSettings(numThreads)))
{
    threadPool.QueueUserWorkItem(() => { ... }));
}
```

This creates a `DedicatedThreadPool` object which allocates a fixed number of threads, each with their own independent task queue.

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