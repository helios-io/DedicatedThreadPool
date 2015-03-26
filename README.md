# DedicatedThreadPool
An instanced, dedicated thread pool for eliminating "noisy neighbor" problems on the CLR `ThreadPool`.

## Installation

You can install `DedicatedThreadPool` via NuGet!

```
PS> Install-package Helios.DedicatedThreadPool
```

This package doesn't install any binaries, just the following C# file: `Helios.Concurrency.DedicatedThreadPool.cs` - which contains the `DedicatedThreadPool`, `DedicatedThreadPoolTaskScheduler`, and `DedicatedThreadPoolSettings` classes.

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

> NOTE: `DedicatedThreadPool` is marked as `internal` by default, so it can be used opaquely across many dependent projects.

## Benchmark

Latest benchmark on our build server (2 core Windows Azure A2 medium)

```xml
[20:27:48][Step 1/1] Comparing Helios.Concurrency.DedicatedThreadPool vs System.Threading.ThreadPool for 10000 items
[20:27:48][Step 1/1] DedicatedThreadFiber.NumThreads: 2
[20:27:48][Step 1/1] System.Threading.ThreadPool
[20:27:48][Step 1/1] 00:00:00.0070000
[20:27:48][Step 1/1] Helios.Concurrency.DedicatedThreadPool
[20:27:49][Step 1/1] 00:00:00.2100000
[20:27:49][Step 1/1] Comparing Helios.Concurrency.DedicatedThreadPool vs System.Threading.ThreadPool for 100000 items
[20:27:49][Step 1/1] DedicatedThreadFiber.NumThreads: 2
[20:27:49][Step 1/1] System.Threading.ThreadPool
[20:27:49][Step 1/1] 00:00:00.0600000
[20:27:49][Step 1/1] Helios.Concurrency.DedicatedThreadPool
[20:27:51][Step 1/1] 00:00:00.1900000
[20:27:51][Step 1/1] Comparing Helios.Concurrency.DedicatedThreadPool vs System.Threading.ThreadPool for 1000000 items
[20:27:51][Step 1/1] DedicatedThreadFiber.NumThreads: 2
[20:27:51][Step 1/1] System.Threading.ThreadPool
[20:27:55][Step 1/1] 00:00:00.6840000
[20:27:55][Step 1/1] Helios.Concurrency.DedicatedThreadPool
[20:27:58][Step 1/1] 00:00:00.5160000
[20:27:58][Step 1/1] Comparing Helios.Concurrency.DedicatedThreadPool vs System.Threading.ThreadPool for 10000000 items
[20:27:58][Step 1/1] DedicatedThreadFiber.NumThreads: 2
[20:27:58][Step 1/1] System.Threading.ThreadPool
[20:28:42][Step 1/1] 00:00:07.1590000
[20:28:42][Step 1/1] Helios.Concurrency.DedicatedThreadPool
[20:29:04][Step 1/1] 00:00:03.7330000
```

## License

See [LICENSE](LICENSE) for details.

Copyright (C) 2015 Roger Alsing, Aaron Stannard