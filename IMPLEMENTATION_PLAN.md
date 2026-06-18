# IMPLEMENTATION_PLAN.md

> Canonical work tracker for this repo (GitHub Issues are disabled). Execution-ordered.
> Agents: pick the lowest-numbered unfinished **NOW** task whose dependencies are met,
> route by `MODE` (see `AGENTS.md`), and check the box only when its Definition of Done
> is met. Park non-NOW work — do not bulldoze priorities.
>
> Last reviewed: 2026-06-17

---

> **✅ After-action resolved (phase2-c4-fix, 2026-06-17):** Task C.4 completed. PoolMeasurementTests
> isolated via `[CollectionDefinition("MeasurementTests", DisableParallelization = true)]`; 3 deterministic
> passes confirmed; memorizer baseline `4cedbe2f` updated to v3 with isolated reading (~14.2–14.5%).
> C.3 was already done. PARK items (perf-gate; final-commit-review gap) await maintainer decision in
> `BACKLOG_PARKING_LOT.md`. Phase 2 Fix-it work is complete.

---

## Fix-it (PR #3 CI failure, ubuntu-latest) — NOW

### Task C.5: De-flake `World_should_not_end_if_exception_thrown_in_user_callback` (racy distinct-thread-count) · `[LOOP-OK]`
**Source:** PR #3 CI — **ubuntu-latest FAILED** (`Expected: 3 / Actual: 2`) while windows-latest passed and local (8-core) passes. Job 82007338923.
**Issue:** `DedicatedThreadPoolTests.cs:77` and `:88` assert `Assert.Equal(numThreads /*3*/, threadIds.Distinct().Count())` — i.e. that **exactly 3 distinct worker threads** handled the queued callbacks. This is **scheduling/core-count dependent**: on the constrained ubuntu runner one warm worker grabbed two of the three callbacks before a third spun up, so only **2** distinct IDs were recorded. It passed elsewhere by luck — a migrated NUnit assumption never analyzed for raciness (the concurrency-citation gap made manifest).
**Real intent:** "the world does not end if a user callback throws" — the pool keeps processing work after exceptions — **not** a specific distinct-thread count.
**Done when:**
- [x] Replace BOTH exact-count assertions with intent-preserving, **core-count-independent** checks: track an execution counter and assert the pool **still ran the good callbacks after the bad ones threw** (survived), and assert distinct-thread count is within `[1, numThreads]`, not `== numThreads`. Do **NOT** merely delete/weaken to green — preserve the "survives exceptions, keeps working" guarantee.
- [x] Cite `analyze-racy-test` / `dotnet-concurrency-specialist` (per the concurrency-citation rule) with the question answered.
- [x] Full suite green deterministically (≥3× locally); reason about / demonstrate core-count independence so it passes on a 2-core runner.
**Verification:** L1 (engineering: build + xUnit green; racy-test review).
**✅ Resolved 2026-06-17 (phase2-c5-fix iter-01):** Replaced both `Assert.Equal(numThreads, threadIds.Distinct().Count())` with `Assert.InRange(threadIds.Distinct().Count(), 1, numThreads)` + separate `badExecutionCount`/`goodExecutionCount` counters; added `SpinWait.SpinUntil` before sanity check and `WaitForThreadsExit(10s)` after `Dispose()` for deterministic drain. Core-count-independence proved: workers never restart after exceptions (try/catch in `RunThread` keeps the loop alive), so max distinct IDs ≤ `numThreads` regardless of CPU count — `[1, 3]` includes the 2-core-runner case (`{2}`). Also fixed `AtomicCounter.Current` to use `Volatile.Read`. Suite green ×3 consecutively. Cited `dotnet-concurrency-specialist` (questions answered: range bound correct, survival proof valid, Volatile.Read needed, SpinWait 5s adequate).

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

### Task C.4: De-flake the idle-CPU / contention harness (process-wide measure under parallel xUnit) · ⚠ OPEN — RUN EXIT ITEM · `[LOOP-OK]`
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
- [x] Make the measurement tests immune to cross-test contamination — e.g. put `PoolMeasurementTests`
      in its own non-parallel collection (`[CollectionDefinition(DisableParallelization = true)]`) or
      otherwise isolate the sample so concurrent test CPU/lock activity cannot inflate it.
- [x] Re-capture the (now isolated) idle-CPU + contention figures from raw harness output and **update**
      the idle-CPU section of memorizer baseline `4cedbe2f` so it reflects the isolated measurement
      (keep the honest "process-wide, includes runner noise" caveat).
- [x] Idle-CPU and contention tests pass deterministically (re-run the full suite ≥3× with no failure).
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

- [x] **C2-1** Align the skipped `Fact`'s `DisplayName` to its method intent. In
      `DedicatedThreadPoolTaskSchedulerTests.cs:28-30` the `DisplayName`
      ("Shouldn't immediately try to schedule all threads") no longer matches the method
      name `Should_only_use_one_thread_for_single_task_request` — a cosmetic carryover
      from the NUnit→xUnit migration. *(Done during C.4 work, phase2-c4-fix iter-01.)*

- [x] **C4-1** Correct the mislabeled "isolated" idle-CPU figure and its wrong root cause.
      *(Source: phase2-c4-fix after-action adversarial review, Finding #1.)* The C.4 fix
      records ~14.2–14.5% as the **"isolated"** idle-CPU reading (memorizer `4cedbe2f` v3
      section "v3 reading (isolated…)"; `91669fb` commit message), and blames the prior 17.4%
      on "SpinWait-heavy siblings." **Both claims are inaccurate** (measured this review): the
      genuinely-isolated reading is **~10.3%** (single test, no other tests in process); the
      recorded ~14% is the **full-suite-with-collection-isolation** number; and running
      `PoolMeasurementTests` **alone with zero siblings still reads 14–16%** (a 2-test in-process
      filter reproduces 17.4%) — so the dominant ~4–7pp is **intra-process runtime churn**
      (JIT/GC/MeterListener/runner threads), **not** sibling parallelism. The `<20%` gate still
      passes deterministically on this box, so this is a record-honesty fix, not a broken gate.
      **This does NOT reopen the C.4 gate** — the gate still passes; only the commit-narrative
      claim and the memorizer `4cedbe2f` v3 wording need correction.
      **Done when:**
      - [x] Edit memorizer `4cedbe2f` v3 idle-CPU section: relabel the ~14% as
            "full-suite (with `DisableParallelization`)" not "isolated"; record the
            truly-isolated ~10.3%; attribute the ~14% floor to intra-process runtime overhead,
            not to SpinWait siblings (keep the honest process-wide caveat).
      - [x] No code change required; the `DisableParallelization` collection stays.
      **✅ Resolved 2026-06-17:** memorizer `4cedbe2f` corrected to v5 (three-way decomposition: true-isolation ≈10.3%; ~14% = intra-process churn, not siblings). Commit `91669fb`'s message is historical, superseded by the corrected record.
      **Verification:** L1 (perf: documented machine, raw output read directly, recorded to memorizer).

- [ ] **C5-1** Tighten or document the near-tautological lower bound in the C.5 de-flake.
      *(Source: phase2-c5-fix after-action adversarial review, CLEANUP #1.)* The de-flake replaced
      the racy equality with `Assert.InRange(threadIds.Distinct().Count(), 1, numThreads)`
      (`DedicatedThreadPoolTests.cs:92,110`). Because `Assert.Equal(numThreads, badExecutionCount.Current)`
      (`:88`) and `Assert.Equal(numThreads * 10, goodExecutionCount.Current)` (`:109`) already prove
      callbacks executed, `distinct >= 1` is automatically true — the lower bound `1` can never fail on
      its own; only the upper bound (`<= numThreads`) carries real information. Harmless redundancy, not
      a behavior risk.
      **Done when:**
      - [ ] Either drop the lower bound to a comment / assert the upper-bound invariant directly
            (`Assert.True(threadIds.Distinct().Count() <= numThreads)`), or document why `1` is the
            intended floor. Fold into the next touch of this file — not worth a loop iteration on its own.
      **Verification:** L1 (engineering: build + xUnit green).

- [ ] **C5-2** Correct the record framing of the `AtomicCounter.Current` `Volatile.Read` change.
      *(Source: phase2-c5-fix after-action adversarial review, CLEANUP #2.)* The C.5 work changed
      `AtomicCounter.Current` to `Volatile.Read(ref _seed)` (`AtomicCounter.cs:23`) and the iter-01
      record frames it as load-bearing for the test's final asserts. It is functionally **redundant for
      those final asserts**: writes go through `Interlocked.Increment` (full barrier) and the final
      counts are read on the test thread *after* `WaitForThreadsExit` (a `Task.WaitAll` acquire barrier),
      so correct values would be observed even without the change. The `Volatile.Read` is a legitimate
      general-purpose hardening of the test utility (the in-loop `SpinUntil` polling read at `:85`
      genuinely benefits on weakly-ordered hardware) — only the iter-01 framing slightly over-states it.
      The change is benign and improving; this is a record-honesty nit, not a correctness problem.
      **Done when:**
      - [ ] If revisited, note in the resolution that `Volatile.Read` hardens the polling read; the
            post-`WaitForThreadsExit` final reads were already correctly ordered. No code change required.
      **Verification:** L1 (engineering: no shipped-source change; test-support file only).

---

## Phase 3.0 — Prep (loop-safe groundwork before the gated core)  ·  MODE=perf  ·  **NOW**

Establishes the comparison rig and targets the gated Phase 3 core will be judged against —
**without** building any new pool. (Loop-safe: benchmarks only EXISTING types.)

### Task P3.0.1: BenchmarkDotNet comparison harness — current pool vs .NET ThreadPool vs DedicatedThreadPoolPipeScheduler · `[LOOP-OK]`
**Source:** Phase 3 prep + StackExchange.Redis #3060 (memorizer `eb4916e3` §4): we must "beat the lock-y 7-year-old `DedicatedThreadPoolPipeScheduler` on 2 cores."
**Scope guardrail (READ):** Benchmark only the **three schedulers that already exist today** — the current `Helios.Concurrency.DedicatedThreadPool` (`QueueUserWorkItem`), `System.Threading.ThreadPool` (`UnsafeQueueUserWorkItem`), and `Pipelines.Sockets.Unofficial.DedicatedThreadPoolPipeScheduler` (`Schedule`). **Do NOT design, stub, or implement the new lock-free pool — that is Phase 3, `[GATED]`.** This task only stands up the measurement rig + records where the *current* landscape sits.
**Done when:**
- [x] Add a BenchmarkDotNet benchmark to the existing `*.Benchmarks` project comparing the three schedulers on a small-work-item throughput workload, at the default thread count **and** a constrained (e.g. 2-thread) config. Add `Pipelines.Sockets.Unofficial` as a **benchmarks-only** PackageReference (CPM) — NOT a dependency of the shipped library.
- [x] `[MemoryDiagnoser]`; smoke-runs under `build.ps1 Benchmark -Smoke` (CI `--job dry`) and runs fully on the dev box.
- [x] Record the preliminary comparison numbers to memorizer (new record, linked to the spec) — clearly labelled preliminary per the baseline discipline in `TOOLING.md`; note this is the "landscape the rewritten pool must beat."
**Verification:** L1 (perf: build + BDN runs; raw output read directly; recorded to memorizer).
**✅ Resolved 2026-06-17 (phase3-prep iter-01):** `SchedulerComparisonBenchmarks.cs` added; `Pipelines.Sockets.Unofficial` v2.2.16 added as benchmarks-only CPM dep; `Program.cs` migrated to `BenchmarkSwitcher` for correct multi-class filtering. ShortRun (3 iter, 3 warmup): Helios 10.5ms ≈ parity with .NET TP (10.0ms, ratio 1.05) at 8w; Helios 7.0ms vs .NET TP 11.4ms (ratio 0.62) at 2w; PipeScheduler 3× slower than .NET TP in both configs with 47K–66K Monitor contentions per run vs 0 for Helios. Landscape recorded to memorizer `1eeb3867-a9af-4d45-b93b-c4a593fcec95` (BASELINE-FOR spec `2c734cfb`).

### Cleanup (phase3-prep after-action — non-blocking, fold into next benchmark/record touch)

*Source: phase3-prep after-action postmortem (`.ralph/runs/phase3-prep/postmortem.md`). No NOW
fix-it was required (overall verdict PARTIAL, driven by process/auditability gaps, not code defects).*

- [x] **C6-1** `[LOOP-OK]` Bound the benchmark's `done.Wait()`. The per-invocation drain in
      `SchedulerComparisonBenchmarks.cs` calls an **unbounded** `done.Wait()`; if a scheduler ever
      drops a work item the harness hangs forever instead of failing loud in CI. Add a timeout +
      throw on the next benchmark touch. Perf/test code only — loop-safe. *(postmortem.md)*
      **Verification:** L1 (perf: build + BDN smoke green).
- [x] **C6-2** `[LOOP-OK]` Restore the `Directory.Packages.props` trailing newline. `bb63a41`
      stripped the final `\n` (and added incidental blank-line churn). Cosmetic hygiene; trivially
      loop-safe. *(postmortem.md)*
      **Verification:** L1 (release: build green; no behavior change).
- [x] **C6-3** Correct the "100000 contentions" label in memorizer `1eeb3867`. The recorded .NET TP
      contention figure equals *exactly* WorkItems (100,000) — almost certainly Interlocked/lock-release
      events mislabeled as `Monitor` contention by the BDN Threading diagnoser. Conclusion unaffected
      (PipeScheduler is the contention loser; Helios = 0); the label merely misleads. **Not `[LOOP-OK]`**
      — requires a memorizer write to a record feeding the gated Phase 3 decision; best batched with the
      PARK-B re-run that rewrites this record. *(postmortem.md)*
      **Verification:** L1 (perf: corrected record read directly, recorded to memorizer).

---

## Phase 3 — Pool core augmentation (re-sequenced 2026-06-18, **interactive / human-reviewed**)  ·  MODE=engineering/perf  ·  **NOW**

**Re-sequenced per the `1eeb3867` finding:** the *current* single-queue pool already ≈ matches/beats
.NET TP on throughput, allocation, and contention — so the value is the **two capabilities it lacks**
(the exact reasons Akka abandoned it: no hill-climbing, inefficient waiting), added to the **current
design**. The higher-risk Chase-Lev work-stealing rewrite is **deferred to Phase 3c**. Built
**interactively with `dotnet-concurrency-specialist` review — NOT via the autonomous loop.**

### Task 3a: Efficient parking — kill the idle-CPU burn  ·  **NOW**
**Problem:** `UnfairSemaphore.Wait` (`…Helios.Concurrency.DedicatedThreadPool.cs:631`) spins via
`Thread.Sleep(0)` (budget `spinLimitPerProcessor=50`, scaled — hundreds of yields) before parking on
the kernel `Semaphore`. N idle workers in that hot Sleep(0) spin = the >16%/node idle-CPU burn
(Akka #4983; our preliminary smoke ~14%). **Keep** the packed-CAS state, spinner-preference, padding,
request counter (digest `eb4916e3` §2.1). **Kill** the Sleep(0) spin. **Replace** with calibrated
hardware-`PAUSE` `Thread.SpinWait` (exp backoff, ~70-norm-spin budget x64 / ×4 ARM) then park.
LIFO blocker-stack wake-order = a *latency* opt, deferred.
**Done when:**
- [ ] `Thread.Sleep(0)` spin replaced with calibrated `Thread.SpinWait` + tightened budget; packed-CAS
      state / spinner-preference / padding / request-counter preserved; net10.0 builds; xUnit green.
- [ ] **Dedicated idle-CPU harness** (resolves the parked process-wide-gauge methodology): a console
      process hosting ONLY the pool, measuring `Environment.CpuUsage` while idle (process CPU ≈ pool
      CPU — no runner contamination). Record before/after to memorizer.
- [ ] Idle CPU drops materially toward ~0 for the parked pool (dedicated harness, A/B).
- [ ] **No throughput regression** vs `1eeb3867` (the comparison benchmark stays green).
**Verification:** L1/perf (build + xUnit; dedicated idle-CPU harness; benchmark no-regression; `dotnet-concurrency-specialist` review).

### Task 3b: Hill-climbing controller + starvation injector  ·  **NEXT**
On the current design. Port `HillClimbing.cs` **per-instance** (digest §2.4 / spec §6.3) + the separate
GateThread **starvation injector** (a blocked worker reports zero completions, so the throughput loop
alone would *remove* threads when it should add). Add the **idle-park timeout** (workers retire on
timeout when over-goal — the hook 3a deliberately left out). Cap by min/max **and cgroup quota**;
parked ≠ blocked.
**Done when:** convergence ≤~2s after a load step (step-response harness, **up 1→C and down C→1**,
anti-oscillation stddev ≤~1); no throughput regression; configurable via settings.
**Verification:** L1/perf (step-response harness; `dotnet-concurrency-specialist` review).

---

## Phase 3c — Chase-Lev work-stealing + affinity (DEFERRED)  ·  MODE=engineering  ·  **LATER** · `[GATED]`

The higher-risk lock-free rewrite (was 3.1 + 3.3). **Deferred** per `1eeb3867`: no throughput gain
justifies its risk now (current pool already ≈ .NET TP); its real value is the **IoExecutor I/O
co-location** (Akka transport) — a later concern. Stays `[GATED]` (silent-failure, ARM64-sensitive):
per-worker **lock-free Chase-Lev deque** (PPoPP'13 C11; signed `long` indices; LIFO-pop/FIFO-steal;
`Interlocked.MemoryBarrier()` seq-cst fence in **both** take & steal; GC-reclaimed buffers; Kestrel
`IOQueue` lost-wakeup guard) + lock-free global queue; **affinity = locality hint** (bias initial
placement, steal-tolerant; ordering is an adapter concern). Cross-cutting traps + acceptance gates
(≤5% parity, fence-correctness on **ARM64**, take-on-empty-vs-steal, `Monitor.Contention`≈0, 2-core +
cgroup scenario) per `PROJECT_CONTEXT.md` + digest `eb4916e3`. **Unlocked only by human review on real
hardware.**

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
