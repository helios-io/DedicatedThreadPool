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

## 🔒 Autonomous loop (RALPH) scope — READ FIRST

Tasks are `- [ ] **N.M**` checkboxes; each phase's `**DoD:**` line is its done-when.

Autonomous iterations (`ralph.sh`) are authorized **only** for tasks tagged
**`[LOOP-OK]`** (currently **2.2, 2.3, 2.4**). **Everything tagged `[GATED]` — all of
Phase 3 onward — is off-limits to the loop:** it's lock-free concurrency with silent,
hardware-dependent failure modes (seq-cst fences, parking, hill-climbing) whose
acceptance gates this CI box **cannot** validate (no ARM64; no governor control;
`perf_event_paranoid=4`).

**Loop rule:** work the first unchecked `[LOOP-OK]` task. If the next unchecked task is
`[GATED]` — or only `[GATED]` tasks remain — **STOP**: write the iter-log explaining the
gate, make **no** changes, do not commit, and exit. Never start, scaffold, or "partially"
do a `[GATED]` task. Never disable/weaken a test or suppress a warning to reach green.
Human review on real hardware unlocks `[GATED]` work.

---

## Phase 0 — Agent OS bootstrap  ·  MODE=release  ·  **NOW**

- [x] `PROJECT_CONTEXT.md`, `TOOLING.md`, `AGENTS.md`, `CLAUDE.md`, this plan.
- [x] Maintainer agrees the OS is correct — confirmed 2026-06-17 ("commit this and start Phase 1").

---

## Phase 1 — Build system modernization (CI/CD)  ·  MODE=release  ·  **NOW**

Model: `akkadotnet/build-system-template`. Goal: green, reproducible, modern build.

> **Status (2026-06-17):** done on branch `modernize/phase-1-build-system`; local
> `build.ps1 Build/Test/Pack` all green on `net10.0` (content-only package verified).
> Deferred as unnecessary right now: **Incrementalist** (single-project repo) and a
> `Docs` build target (no `docs/` site yet). CI to be validated by the PR run.

- [x] **1.1** Add `global.json` pinning the .NET SDK (10.0.x, `rollForward: latestfeature`).
- [x] **1.2** Add root `Directory.Build.props` (copyright, SourceLink, deterministic
      build, `LangVersion`, shared metadata) and migrate `src/common.props` into it.
- [x] **1.3** Add `Directory.Packages.props` (Central Package Management); move all
      `PackageReference` versions out of csproj files.
- [x] **1.4** Add `.config/dotnet-tools.json` (Incrementalist, docfx) + `NuGet.Config`;
      delete vendored `src/.nuget/` (`NuGet.exe`, targets).
- [x] **1.5** Convert `src/*.sln` → `.slnx` (modern solution format).
- [x] **1.6** Author `build.ps1` (pwsh) with targets: `Restore`, `Build`, `Test`,
      `Nbench`→`Benchmark`, `Pack`, `Docs` — replacing `build.fsx`/`.cmd`/`.sh`/FAKE.
      Read version from `RELEASE_NOTES.md` (keep that convention).
- [x] **1.7** Add `.github/workflows/pr-validation.yml` — build + test on `net10.0`,
      on **Linux + Windows**.
- [x] **1.8** Add `.github/workflows/release.yml` — tag-driven `pack` + `nuget push`
      (API key from repo secret). Add `coverlet.runsettings`.
- [x] **1.9** Remove legacy: `build.fsx`, `build.cmd`, `build.sh`, `build-system/*.yaml`.

**DoD:** `dotnet build` + `dotnet test` green from a clean clone via `build.ps1` and via
the new Actions workflow on Linux and Windows; `pack` produces a content-only package
identical in shape to today's; no FAKE/vendored-nuget remnants; `build.fsx`'s stale
`Akka.Streams.Kafka` identity is gone.

---

## Phase 2 — Test + benchmark stack migration & baseline  ·  MODE=engineering/perf  ·  **NEXT**

- [x] **2.1** Migrate the test project NUnit → **xUnit** (keep all existing assertions);
      target a runner that exercises the library compiled for `net10.0`.
      *(Done: xUnit v2 + VSTest + coverlet; 4 pass / 1 skip, 0 warnings on net10.0.)*
- [x] **2.2** `[LOOP-OK]` Replace the NBench perf project with a **BenchmarkDotNet** project
      (`*.Benchmarks`). Port the existing throughput benchmark (Helios pool vs
      `System.Threading.ThreadPool`).
      *(Done: BDN 0.14.0; ThroughputBenchmarks.cs with [MemoryDiagnoser]; CI smoke via --job dry;
      4 pass / 1 skip, 0 warnings on net10.0.)*
- [ ] **2.3** `[LOOP-OK]` Capture a **preliminary** baseline of the *current* pool
      (throughput, alloc, idle CPU) **on this box**, recorded to memorizer. *(The OFFICIAL
      baseline — cooled bare-metal, `governor=performance`, ≥3 reps per `dae34f6d` — is
      `[GATED]`; this preliminary run is for harness shakeout, clearly labelled as such.)*
- [ ] **2.4** `[LOOP-OK]` Build the **measurement scaffolding** the Phase 3–4 gates depend on,
      validated against the CURRENT pool: an idle-CPU harness via `Environment.CpuUsage`
      (assert ≈0 when idle), a `Monitor.Contention`≈0 check, and EventCounters/Meters stubs
      (active-worker / park / wake counts). Local/preliminary numbers only — governed,
      bare-metal, and ARM64 runs are `[GATED]`.

**DoD:** xUnit suite green; BenchmarkDotNet runs locally and in CI (smoke); idle-CPU +
contention harness compile and pass against the current pool; a preliminary, reproducible
baseline is recorded for the pre-rewrite pool.

---

## Phase 3 — Pool core rewrite (IoExecutor spec P1)  ·  MODE=engineering  ·  **NEXT** · `[GATED]`

Build the reusable core at **fixed** thread count first; prove parity before adapting.

- [ ] **3.1** Per-worker **lock-free Chase-Lev deque** (PPoPP'13 C11 formulation; **signed
      `long` monotonic indices**; LIFO-pop owner / FIFO-steal thief) + lock-free **global**
      queue. Memory ordering: `Interlocked.MemoryBarrier()` in **both** take & steal (the
      seq-cst fence). Missed-steal → re-request a worker; GC reclaims grown buffers; batch-
      drain per wake with the **Kestrel `IOQueue` lost-wakeup guard**.
- [ ] **3.2** **Efficient parking (low idle CPU):** calibrated `SpinWait` (PAUSE, ~70 spins
      x64 / ×4 ARM) checking local/global/steal, then park on a **managed LIFO blocker-stack**
      (warmest wakes first). **Wake exactly one** per work unit; "no-spin hint"; 20s idle
      timeout. **No `Thread.Sleep(0)` spin; no single shared `Monitor`.**
- [ ] **3.3** **Affinity = locality hint** (per locked decision): bias a key's *initial
      placement* to `hash(key) % workers`; the item stays steal-eligible. **No** rebalanced
      pinning and **no** FIFO-lane layer in the core (ordering is an adapter concern).
- [ ] **3.4** Bench/profile vs `System.Threading.ThreadPool`: CPU-bound microbench **≤5%**;
      **idle-CPU gate** (`Environment.CpuUsage` ≈0 when idle); **`Monitor.Contention` ≈0**;
      **2-core + cgroup-quota** small-message-storm scenario (beat `DedicatedThreadPoolPipe-
      Scheduler`); lock-free-vs-spinlock-steal benchmark (open question).

**DoD:** compiles for `net10.0` from the single source file; xUnit green incl. **take-on-empty
racing steal** and **fence-correctness stress tests run on ARM64** (plus exception isolation,
ordering, dispose/drain; racy review via `analyze-racy-test`); microbench parity ≤5% + idle-CPU
+ `Monitor.Contention`≈0 recorded; no dead-end from PROJECT_CONTEXT re-introduced.

---

## Phase 4 — Hill-climbing controller **+ starvation injector** (spec P2)  ·  MODE=engineering/perf  ·  **NEXT** · `[GATED]`

Two cooperating control loops — the throughput controller alone **cannot** react to blocking
(a blocked worker reports zero completions, so it would *remove* threads when it should add).

- [ ] **4.1** Port the runtime `HillClimbing.cs` algorithm **per-instance** (square-wave probe
      + Goertzel transfer-function gradient + confidence/SNR + `pow(move,2)` gain; randomized
      10–200ms sample interval). Params per the digest (`eb4916e3`) / spec §6.3.
- [ ] **4.2** **Separate starvation/blocking injector** (GateThread, ~500ms; owns cold-start
      ramp): inject on queue-non-drainage + blocked-thread count, CPU-aware. Expose
      `NotifyBlocked/Unblocked` hooks (**defer** the full cooperative-blocking ramp). Cap by
      `min`/`max` **and cgroup CPU quota**. Parked ≠ blocked; stamp `lastDequeue` on steals.
- [ ] **4.3** Validate **convergence ≤~2s** after a load step (step-response harness, **up
      1→C and down C→1**, anti-oscillation stddev ≤~1) and **microbench parity** ≤5% of .NET
      TP (acceptance gate **G8**).

**DoD:** convergence (up + down) + parity + anti-oscillation demonstrated and recorded;
configurable via settings; cgroup-quota-aware; no busy-spin waste and no park/wake regression
vs Phase 3.

---

## Phase 5 — Framework-agnostic adapters  ·  MODE=engineering  ·  **LATER** · `[GATED]`

- [ ] **5.1** Modernize `DedicatedThreadPoolTaskScheduler` over the new core.
- [ ] **5.2** Add a `SynchronizationContext` adapter.
- [ ] **5.3** Add `IThreadPoolWorkItem` fast-path scheduling (net10.0).
- [ ] **5.4** **No** Akka (or other framework) dependency — adapters stay generic.

**DoD:** adapters covered by xUnit; public/internal surface intentional; API-break review
(`check-api-breaking`) clean vs prior content-shipped API.

---

## Phase 6 — Release  ·  MODE=release  ·  **LATER** · `[GATED]`

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
