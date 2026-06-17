# IMPLEMENTATION_PLAN.md

> Canonical work tracker for this repo (GitHub Issues are disabled). Execution-ordered.
> Agents: pick the lowest-numbered unfinished **NOW** task whose dependencies are met,
> route by `MODE` (see `AGENTS.md`), and check the box only when its Definition of Done
> is met. Park non-NOW work — do not bulldoze priorities.
>
> Last reviewed: 2026-06-17

## Sequencing rationale

Modernize the build **first** so the rewrite is testable/benchmarkable; **then**
capture honest baselines on the *old* pool; **then** rewrite the pool core and prove
parity before adding hill-climbing; **then** adapters; **then** release. The pool
rewrite (Phases 3–4) is gated on Phases 1–2 because we cannot prove "no regression"
without a modern benchmark harness and a recorded baseline.

---

## Phase 0 — Agent OS bootstrap  ·  MODE=release  ·  **NOW**

- [x] `PROJECT_CONTEXT.md`, `TOOLING.md`, `AGENTS.md`, `CLAUDE.md`, this plan.
- [ ] Maintainer agrees the OS is correct (completes the bootstrap).

---

## Phase 1 — Build system modernization (CI/CD)  ·  MODE=release  ·  **NOW**

Model: `akkadotnet/build-system-template`. Goal: green, reproducible, modern build.

- [ ] **1.1** Add `global.json` pinning the .NET SDK (10.0.x, `rollForward: latestfeature`).
- [ ] **1.2** Add root `Directory.Build.props` (copyright, SourceLink, deterministic
      build, `LangVersion`, shared metadata) and migrate `src/common.props` into it.
- [ ] **1.3** Add `Directory.Packages.props` (Central Package Management); move all
      `PackageReference` versions out of csproj files.
- [ ] **1.4** Add `.config/dotnet-tools.json` (Incrementalist, docfx) + `NuGet.Config`;
      delete vendored `src/.nuget/` (`NuGet.exe`, targets).
- [ ] **1.5** Convert `src/*.sln` → `.slnx` (modern solution format).
- [ ] **1.6** Author `build.ps1` (pwsh) with targets: `Restore`, `Build`, `Test`,
      `Nbench`→`Benchmark`, `Pack`, `Docs` — replacing `build.fsx`/`.cmd`/`.sh`/FAKE.
      Read version from `RELEASE_NOTES.md` (keep that convention).
- [ ] **1.7** Add `.github/workflows/pr-validation.yml` — build + test on `net10.0`,
      on **Linux + Windows**.
- [ ] **1.8** Add `.github/workflows/release.yml` — tag-driven `pack` + `nuget push`
      (API key from repo secret). Add `coverlet.runsettings`.
- [ ] **1.9** Remove legacy: `build.fsx`, `build.cmd`, `build.sh`, `build-system/*.yaml`.

**DoD:** `dotnet build` + `dotnet test` green from a clean clone via `build.ps1` and via
the new Actions workflow on Linux and Windows; `pack` produces a content-only package
identical in shape to today's; no FAKE/vendored-nuget remnants; `build.fsx`'s stale
`Akka.Streams.Kafka` identity is gone.

---

## Phase 2 — Test + benchmark stack migration & baseline  ·  MODE=engineering/perf  ·  **NEXT**

- [ ] **2.1** Migrate the test project NUnit → **xUnit** (keep all existing assertions);
      target a runner that exercises the library compiled for `net10.0`.
- [ ] **2.2** Replace the NBench perf project with a **BenchmarkDotNet** project
      (`*.Benchmarks`). Port the existing throughput benchmark (Helios pool vs
      `System.Threading.ThreadPool`).
- [ ] **2.3** Capture and record a **baseline** of the *current* pool (throughput, alloc,
      park/wake) on a documented machine, following memorizer `dae34f6d` discipline.
      Write the baseline back to memorizer.

**DoD:** xUnit suite green; BenchmarkDotNet runs locally and in CI (smoke); a recorded,
reproducible baseline exists for the pre-rewrite pool.

---

## Phase 3 — Pool core rewrite (IoExecutor spec P1)  ·  MODE=engineering  ·  **NEXT (gated on 1–2)**

Build the reusable core at **fixed** thread count first; prove parity before adapting.

- [ ] **3.1** Per-worker **local LIFO deque** (Chase-Lev style) + shared **global MPMC**
      queue; idle workers **work-steal** (steal from FIFO end). Batch-drain (K items/wake).
- [ ] **3.2** **Efficient parking:** adaptive spin-then-park on a **LIFO** semaphore
      (warmest thread wakes first), checking local/global/steal before parking, using
      modern `net10.0` intrinsics directly.
- [ ] **3.3** Optional **affinity key** on enqueue (`Schedule(work, affinityKey)`) pinning
      related work to one warm worker; rebalance mapping when active-count changes.
- [ ] **3.4** Microbenchmark vs `System.Threading.ThreadPool` — CPU-bound **and**
      I/O-bound-shaped. **Must match within ~5%** before proceeding.

**DoD:** compiles for `net10.0` from the single source file; xUnit green (incl. exception
isolation, ordering, dispose/drain, racy-test review via `analyze-racy-test`); microbench
parity ≤5% recorded; no dead-end from PROJECT_CONTEXT re-introduced.

---

## Phase 4 — HillClimbingController (spec P2)  ·  MODE=engineering/perf  ·  **NEXT (gated on 3)**

- [ ] **4.1** Port/adapt the runtime `HillClimbing.cs` algorithm (sinusoidal wave →
      throughput-gradient estimate → move active count; clamp Δ/sample). Params per spec §6.3.
- [ ] **4.2** Blocking-detection injection path (inject on starvation when workers park
      on I/O), bounded by `min`/`max` thread caps.
- [ ] **4.3** Validate **convergence** (≤~2s to optimal active count after a load step)
      and **microbench parity** within ~5% of .NET TP (acceptance gate **G8**).

**DoD:** convergence + parity demonstrated and recorded; configurable via settings; no
busy-spin waste and no slow park/wake regressions vs Phase 3.

---

## Phase 5 — Framework-agnostic adapters  ·  MODE=engineering  ·  **LATER**

- [ ] **5.1** Modernize `DedicatedThreadPoolTaskScheduler` over the new core.
- [ ] **5.2** Add a `SynchronizationContext` adapter.
- [ ] **5.3** Add `IThreadPoolWorkItem` fast-path scheduling (net10.0).
- [ ] **5.4** **No** Akka (or other framework) dependency — adapters stay generic.

**DoD:** adapters covered by xUnit; public/internal surface intentional; API-break review
(`check-api-breaking`) clean vs prior content-shipped API.

---

## Phase 6 — Release  ·  MODE=release  ·  **LATER**

- [ ] **6.1** Update `README.md` (new capabilities, benchmarks) and `RELEASE_NOTES.md`.
- [ ] **6.2** Decide version bump (likely `1.0.0` given the rewrite) and namespace/notes.
- [ ] **6.3** Cut release via Actions; verify the published content-only package.

**DoD:** release notes accurate; package validated by a clean consume-and-compile test on
`net10.0`; benchmarks published in README.

---

## Parked / out of scope for this repo

- Akka facets — Dispatcher, PipeScheduler, raw-socket completion routing (spec P3–P5),
  and goals G1–G7/G9 — belong in **akka.net**, not here. This repo delivers the standalone
  primitive only.
