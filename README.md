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
[04:01:18][Step 1/1] Comparing Helios.Concurrency.DedicatedThreadPool vs System.Threading.ThreadPool for 10000 items
[04:01:18][Step 1/1] DedicatedThreadFiber.NumThreads: 2
[04:01:18][Step 1/1] System.Threading.ThreadPool
[04:01:18][Step 1/1] 00:00:00.0060000
[04:01:18][Step 1/1] Helios.Concurrency.DedicatedThreadPool
[04:01:19][Step 1/1] 00:00:00.0100000
[04:01:19][Step 1/1] Comparing Helios.Concurrency.DedicatedThreadPool vs System.Threading.ThreadPool for 100000 items
[04:01:19][Step 1/1] DedicatedThreadFiber.NumThreads: 2
[04:01:19][Step 1/1] System.Threading.ThreadPool
[04:01:19][Step 1/1] 00:00:00.0520000
[04:01:19][Step 1/1] Helios.Concurrency.DedicatedThreadPool
[04:01:19][Step 1/1] 00:00:00.0420000
[04:01:19][Step 1/1] Comparing Helios.Concurrency.DedicatedThreadPool vs System.Threading.ThreadPool for 1000000 items
[04:01:19][Step 1/1] DedicatedThreadFiber.NumThreads: 2
[04:01:19][Step 1/1] System.Threading.ThreadPool
[04:01:23][Step 1/1] 00:00:00.6630000
[04:01:23][Step 1/1] Helios.Concurrency.DedicatedThreadPool
[04:01:26][Step 1/1] 00:00:00.4290000
[04:01:26][Step 1/1] Comparing Helios.Concurrency.DedicatedThreadPool vs System.Threading.ThreadPool for 10000000 items
[04:01:26][Step 1/1] DedicatedThreadFiber.NumThreads: 2
[04:01:26][Step 1/1] System.Threading.ThreadPool
[04:02:14][Step 1/1] 00:00:08.0290000
[04:02:14][Step 1/1] Helios.Concurrency.DedicatedThreadPool
[04:02:41][Step 1/1] 00:00:04.5240000
```

## License

See [LICENSE](LICENSE) for details.

Copyright (C) 2015 Roger Alsing, Aaron Stannard