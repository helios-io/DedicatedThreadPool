# IMPLEMENTATION_PLAN.md

> Canonical work tracker for this repo (GitHub Issues are disabled). Execution-ordered.
> Agents: pick the lowest-numbered unfinished **NOW** task whose dependencies are met,
> route by `MODE` (see `AGENTS.md`), and check the box only when its Definition of Done
> is met. Park non-NOW work — do not bulldoze priorities.
>
> Last reviewed: 2026-06-17

---

> **⚠ After-action (phase2-loop, 2026-06-17): run ended with an OPEN NOW item.**
> The loop terminated after iter-04 (C.3, done) without executing **Task C.4** (racy
> idle-CPU/contention harness). C.4 below is the **top-priority unresolved item** and the
> **entry point for the next run** — pick it up first. Postmortem reproduced the flakiness
> (idle-CPU 10.3% isolated → 15.9% under the full parallel suite; contention delta 0 → 7).
> See `.ralph/runs/phase2-loop/postmortem.md`. C.3 is complete (verified). PARK items
> (perf-gate; final-commit-review gap) await maintainer decision in `BACKLOG_PARKING_LOT.md`.

---

## Fix-it (Review after iter-03) — NOW

### Task C.3: Move `PoolMetrics` into the single shipped source file (locked-decision conflict)
**Source:** Review after iteration 3, finding #1 (Architecture / locked-decision compliance).
**Issue:** PROJECT_CONTEXT.md (line 78, **locked decision**) requires the library to "source-ship as
**one `.cs` file**" via a content-only package. The core `.csproj` packs **only**
`Helios.Concurrency.DedicatedThreadPool.cs` (`<Content Include=... Pack="true">`). Task 2.4 added a
**second** core file `PoolMetrics.cs` that is (a) **not** in the content pack and (b) explicitly the
"Phase 3–4 wiring point" the pool will reference. Today the package still compiles for consumers
(nothing in the shipped file references `PoolMetrics` yet — verified by grep), but the moment Phase 3
wires `PoolMetrics.*` into `Helios.Concurrency.DedicatedThreadPool.cs`, source-ship consumers will get
a missing-type compile error because `PoolMetrics.cs` is never shipped. This is a latent packaging
break sitting in tension with a locked decision.
**Done when:**
- [x] Move the `internal static class PoolMetrics` (Meter + 3 instruments) **into** the single shipped
      file `src/core/Helios.DedicatedThreadPool/Helios.Concurrency.DedicatedThreadPool.cs` (it is
      `internal`, consistent with the "internal types" source-ship model).
- [x] Delete `src/core/Helios.DedicatedThreadPool/PoolMetrics.cs`.
- [x] `PoolMetrics_PublishesExpectedInstruments` still passes (instrument names unchanged).
- [x] Build 0/0 and full xUnit suite green on `net10.0`.
**Verification:** L1 (engineering: build + xUnit green; no UI/IO).

### Task C.4: De-flake the idle-CPU / contention harness (process-wide measure under parallel xUnit) · ⚠ OPEN — RUN EXIT ITEM
**Source:** Review after iteration 3, finding #2 (Regression risk / racy test).
**After-action escalation (phase2-loop, 2026-06-17):** the loop ended with this NOW item
**unexecuted** — iter-04 honestly deferred it ("to iteration 05") but iteration 05 never
ran, so the known-flaky test still ships. The postmortem independently re-reproduced the
contamination: **idle-CPU 10.3% isolated → 15.9% under the full parallel suite** (a 5.6pp
swing, only ~4pp below the `< 0.20` gate) and **contention delta 0 → 7**. This is the
first task the next run must pick up.
**Issue:** `IdlePool_CpuUsage_IsNearZero` samples **process-wide** `Environment.CpuUsage` over a 2 s
window and asserts `cpuFraction < 0.20`, but xUnit runs test classes **in parallel by default** (no
`DisableTestParallelization` / `xunit.runner.json` exists) and the sibling tests
(`DedicatedThreadPoolTests`, `DedicatedThreadPoolTaskSchedulerTests`) burn CPU via
`SpinWait.SpinUntil`. Measured contamination this review: **isolated = 9.8%**, **full parallel suite =
14.6%**, **iter-03 recorded = 17.4%** — the "idle" reading swings ~7.6pp on parallel load alone, with a
margin as thin as 2.6pp below the 20% gate → nondeterministic CI failure. The same flaw hits the
contention harness (`delta=0` isolated vs `delta=4` observed under the parallel scheduler tests, which
use `lock`/`Monitor`); it doesn't fail only because it asserts nothing on the delta.
**Done when:**
- [ ] Make the measurement tests immune to cross-test contamination — e.g. put `PoolMeasurementTests`
      in its own non-parallel collection (`[CollectionDefinition(DisableParallelization = true)]`) or
      otherwise isolate the sample so concurrent test CPU/lock activity cannot inflate it.
- [ ] Re-capture the (now isolated) idle-CPU + contention figures from raw harness output and **update**
      the idle-CPU section of memorizer baseline `4cedbe2f` so it reflects the isolated measurement
      (keep the honest "process-wide, includes runner noise" caveat).
- [ ] Idle-CPU and contention tests pass deterministically (re-run the full suite ≥3× with no failure).
**Verification:** L1 (perf: documented machine, raw output read directly, recorded to memorizer).

---

## Fix-it (Review after iter-02) — NOW

### Task C.2: Capture the missing idle-CPU dimension of the 2.3 preliminary baseline
**Source:** Review after iteration 2, finding #1 (Checkbox Integrity).
**Issue:** Task 2.3's done-when explicitly lists capturing **(throughput, alloc, idle CPU)** for
the current pool's preliminary baseline. The iteration captured **throughput + alloc** (memorizer
`4cedbe2f`) but **not idle CPU**, and the omission was never acknowledged in the iter-02 log — the
box was checked with only two of three dimensions delivered.
**Dependency:** idle-CPU measurement needs the `Environment.CpuUsage` harness that **Task 2.4**
builds. Do **not** attempt this before 2.4 — execute it *as part of* Task 2.4 (building that harness
is the means by which this fix-it is satisfied).
**Done when:**
- [x] While doing Task 2.4, capture a preliminary idle-CPU number for the **current** pool on this
      box (≈0 expected when idle), read from raw harness output.
- [x] Append that idle-CPU figure to the **existing** baseline record memorizer `4cedbe2f` (edit the
      same record — do **not** create a competing baseline), so the 2.3 baseline finally covers all
      three listed dimensions (throughput, alloc, idle CPU).
**Verification:** L1 (perf: documented machine, raw output read directly, recorded to memorizer).

---

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
- [x] **2.3** `[LOOP-OK]` Capture a **preliminary** baseline of the *current* pool
      (throughput, alloc, idle CPU) **on this box**, recorded to memorizer. *(The OFFICIAL
      baseline — cooled bare-metal, `governor=performance`, ≥3 reps per `dae34f6d` — is
      `[GATED]`; this preliminary run is for harness shakeout, clearly labelled as such.)*
      *(Done: ShortRun BDN job, i9-9900K/8c ubuntu24-dev, net10.0; Helios 14.1ms/0B vs .NET TP
      16.2ms/3.2MB per 100K items; memorizer record `4cedbe2f`.)*
- [x] **2.4** `[LOOP-OK]` Build the **measurement scaffolding** the Phase 3–4 gates depend on,
      validated against the CURRENT pool: an idle-CPU harness via `Environment.CpuUsage`
      (assert ≈0 when idle), a `Monitor.Contention`≈0 check, and EventCounters/Meters stubs
      (active-worker / park / wake counts). Local/preliminary numbers only — governed,
      bare-metal, and ARM64 runs are `[GATED]`. **Also append the preliminary idle-CPU number for
      the current pool to memorizer baseline `4cedbe2f` — this closes the idle-CPU dimension of
      Task 2.3 (see Fix-it C.2 at the top of this file).**
      *(Done: `PoolMetrics.cs` stubs (Meter + 3 counters); `PoolMeasurementTests.cs` with idle-CPU
      harness [4.4%/thread, 17.4% process-wide], Monitor.Contention=0 harness, and metrics smoke
      test; all 7 tests green + 1 pre-existing skip on net10.0; idle-CPU appended to memorizer
      `4cedbe2f` v2.)*

**DoD:** xUnit suite green; BenchmarkDotNet runs locally and in CI (smoke); idle-CPU +
contention harness compile and pass against the current pool; a preliminary, reproducible
baseline is recorded for the pre-rewrite pool.

### Cleanup (non-blocking — opportunistic during a future test touch)

- [ ] **C2-1** Align the skipped `Fact`'s `DisplayName` to its method intent. In
      `DedicatedThreadPoolTaskSchedulerTests.cs:28-30` the `DisplayName`
      ("Shouldn't immediately try to schedule all threads") no longer matches the method
      name `Should_only_use_one_thread_for_single_task_request` — a cosmetic carryover
      from the NUnit→xUnit migration. *(Source: postmortem phase2-loop, triage J1-2.
      Cosmetic only; do during the C.4 work or any future test touch.)*

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
