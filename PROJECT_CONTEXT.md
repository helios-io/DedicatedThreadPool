# PROJECT_CONTEXT.md

> Mutable project understanding for agents. This file evolves. The stable,
> harness-loaded constitution is `AGENTS.md` — keep volatile detail *here*, not there.
>
> Last reviewed: 2026-06-17

## What this project is

`Helios.DedicatedThreadPool` is a small concurrency primitive: an **instanced,
dedicated thread pool** that isolates work onto its own fixed set of threads so a
noisy workload cannot starve the shared CLR `ThreadPool` (the "noisy neighbor"
problem). It ships as a **single source file** (`Helios.Concurrency.DedicatedThreadPool.cs`)
via a content-only NuGet package, with types `internal` by default so it can be
embedded opaquely across dependent projects.

Public surface today:

- `Helios.Concurrency.DedicatedThreadPool` — the pool (`QueueUserWorkItem`, `Dispose`).
- `Helios.Concurrency.DedicatedThreadPoolTaskScheduler` — TPL `TaskScheduler` facade.
- `Helios.Concurrency.DedicatedThreadPoolSettings` — config (thread count, name,
  deadlock timeout, exception handler).
- Internal: `UnfairSemaphore` (ported from legacy CoreCLR `win32threadpool.h`),
  a single shared `ConcurrentQueue` work queue, per-thread consuming workers.

## Why it matters / history

Akka.NET once used this pool for system actors, then **retreated to the .NET
`ThreadPool`** because the dedicated pool had two fatal gaps:

1. **No hill-climbing** — thread count is fixed; it cannot adapt to load.
2. **Inefficient waiting** — no modern adaptive spin-then-park / LIFO-warm-thread
   wake discipline, and no work-stealing.

Closing those two gaps is the entire reason this project is being revived.

## Current architecture limits (the work to be done)

- **Fixed thread count.** No thread injection / hill-climbing controller.
- **One shared global queue.** No per-worker local (LIFO) deques, no work-stealing,
  no per-connection/per-key affinity.
- **Dated waiting.** `UnfairSemaphore` spin-then-park is serviceable but predates
  the runtime's `LowLevelLifoSemaphore` + adaptive-spin design.
- **Legacy toolchain.** FAKE 4.61.2 `build.fsx` (still self-identifies as
  `Akka.Streams.Kafka` — copy-paste residue), vendored `nuget.exe` v4.0.0, Azure
  Pipelines on dead images (`vs2017-win2016`, `ubuntu-16.04`). No `global.json`,
  `Directory.Build.props`, CPM, or GitHub Actions.

## The two-part goal

1. **Modernize to 2026 standards** modeled on
   [`akkadotnet/build-system-template`](https://github.com/akkadotnet/build-system-template):
   `.slnx`, `Directory.Build.props`, central package management
   (`Directory.Packages.props`), `global.json`, `.config/dotnet-tools.json`,
   GitHub Actions CI/CD, a `pwsh` `build.ps1` (retiring FAKE), coverlet coverage.
2. **Deliver a genuinely modern dedicated pool** that **hill-climbs**, **waits
   efficiently**, and **work-steals with affinity** — i.e. phases **P1–P2 of the
   IoExecutor spec** (the reusable pool core + `HillClimbingController`). This repo
   owns the *standalone primitive*; Akka-specific facets (Dispatcher, PipeScheduler,
   socket-completion routing — spec P3–P5) live in the Akka.NET repo, not here.

## Locked decisions (2026-06-17)

| Decision | Choice | Consequence |
|---|---|---|
| **Repo scope** | Standalone primitive **+ framework-agnostic adapters** (TaskScheduler, SynchronizationContext, `IThreadPoolWorkItem`). **No** Akka dependency. | Akka integration stays in akka.net and consumes this. |
| **Branding/packaging** | **Keep** `Helios.Concurrency` namespace **+ source-ship** (content-only NuGet, `internal` types). | Backward compatible; distribution model unchanged. |
| **Target frameworks** | **`net10.0` only** (netstandard2.0 **deferred**, not abandoned). | Free use of modern runtime APIs/intrinsics; no cross-TFM `#if` branching for now. |
| **Test/bench/CI** | **xUnit + BenchmarkDotNet + GitHub Actions** (migrating from NUnit/NBench/Azure). | Aligns with the dotnet-skills + build-system-template tooling. |

### Target-framework note (netstandard2.0 deferred)

To de-risk the rewrite, the library targets **`net10.0` only for now**;
`netstandard2.0` is **deferred, not abandoned**. While single-target:

- Use modern runtime APIs/intrinsics freely (modern parking, `Thread.UnsafeStart`,
  hardware `Pause`, newer `Interlocked`/`Volatile`) with **no `#if` branching**.
- The library still source-ships as one `.cs` file via a content-only package.

**Tradeoff to remember:** writing freely against `net10.0` means re-adding
`netstandard2.0` later will require retrofitting polyfills / `#if NET10_0_OR_GREATER`
guards around those modern APIs. Keep hot-path API choices loosely noted so that
retrofit stays mechanical if portability is reinstated.

## Acceptance bar for the new pool (summary)

The standalone primitive must, on a CPU-bound microbenchmark, **match the .NET
`ThreadPool` throughput within ~5%** (prove no compute regression), demonstrate
**hill-climbing convergence** to an optimal active-thread count within ~2s of a load
step, and show **park→run latency ≤ the .NET ThreadPool's**. Full goal set (G1–G9,
which include the Akka-transport-level goals owned by the akka.net repo) lives in the
spec; the repo-local subset is G8 (convergence + microbench parity) and the parking /
work-stealing correctness gates.

## Authoritative external knowledge (memorizer)

The implementation-grade design lives in the **memorizer** MCP server, not in this
repo. Agents doing pool/perf work **must** consult it:

- `2c734cfb-0fb3-42e2-a855-fbea6a56ce83` — **the IoExecutor spec** (full design,
  algorithms, API, goals G1–G9, validation protocol, phases P1–P6, risks/dead-ends).
- Referenced by the spec: `eb52d56b` (profiling proof), `8ce6bcc0` (condensed plan),
  `dae34f6d` (**benchmark discipline** — read before any perf claim), and rejected
  levers `73857988`, `b856f384`, `4d6e4f93`, `741216a6` (do **not** re-attempt these).

## Known dead-ends (do not retry — from memorizer)

- Do **not** remove read-ahead pumps (−2.6x; `73857988`).
- Do **not** use `PipeScheduler.Inline` or hand-roll an SPSC pipe (−45%; `b856f384`).
- Do **not** over-thread (DotNetty-style) nor under-thread (starvation).
- Outbound stream-source coalescing is dead (`4d6e4f93`); stage fusion targets the
  wrong layer (`741216a6`). (These are Akka-transport concerns, listed for completeness.)

## Repo facts agents rely on

- Default branch: `dev`. Remote: `github.com/Aaronontheweb/DedicatedThreadPool`
  (`origin`); `github.com/helios-io/DedicatedThreadPool` (`upstream`).
- **GitHub Issues are disabled** on this repo → `IMPLEMENTATION_PLAN.md` is the
  canonical work tracker, not Issues.
- License: Apache 2.0. Copyright Roger Alsing, Aaron Stannard, Jeff Cyr.
