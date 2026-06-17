# Backlog Parking Lot

> Items parked here need a human decision before they can be worked on.
> RALPH loops do NOT pick up items from this file — only from `IMPLEMENTATION_PLAN.md`.
>
> Each item includes: what it is, where it came from, and what decision is needed.

---

## Items Awaiting Decision

<!-- Add parked items below. Example:

### Extract hardcoded brand colors to config
- **Source:** RALPH run 20260201-143022, iteration 5
- **Issue:** Brand colors are hardcoded in 3 CSS files. Should they be CSS variables or config?
- **Decision needed:** Design decision — CSS custom properties vs theme config file
- **Date parked:** 2026-02-01

-->

### No automated perf-regression gate after the NBench → BenchmarkDotNet port
- **Source:** RALPH run phase2-loop, iteration 1 (Task 2.2); adversarial review after iter-01, finding #2
- **Issue:** The old NBench benchmark enforced a hard throughput floor —
  `[CounterThroughputAssertion(BenchmarkCounterName, MustBe.GreaterThan, 1_000_000)]`
  (1M ops/sec) — so a throughput regression failed the build. The BenchmarkDotNet
  port (`ThroughputBenchmarks.cs`) carries `[MemoryDiagnoser]` and a `[Benchmark(Baseline=true)]`
  ratio but **no pass/fail threshold**. CI runs `--job dry` (smoke only, no statistics),
  so between now and the GATED parity gate (G8, ≤5% vs .NET TP in Phase 3/4) there is **no**
  automated guard against a throughput/alloc regression in the current pool.
- **Decision needed:** Strategy call. Re-introducing a hardcoded throughput floor on CI
  contradicts the project's perf discipline (memorizer `dae34f6d`; IMPLEMENTATION_PLAN states
  "this CI box cannot validate" perf gates: no governor control, `perf_event_paranoid=4`),
  so the loop should NOT autonomously re-add one. Options for a human to choose:
  (a) accept the gap — rely solely on the GATED G8 parity gate on real hardware;
  (b) add a *soft*, generously-bounded local-only guard (e.g. assert HeliosPool ratio ≤ N×
  baseline) that runs in Task 2.3/2.4's full benchmark, never in CI dry runs;
  (c) defer entirely until the pool rewrite (Phase 3) wires up the real acceptance gates.
- **Date parked:** 2026-06-17

### Last work commit of a run gets no mid-loop review (review-stack structural gap)
- **✅ RESOLVED 2026-06-17 (maintainer sign-off):** chose **option (a)** — codified that the
  after-action postmortem MUST deep-review the run's final work commit (review areas A–K).
  Applied additively to `.claude/skills/ralph-output-adversarial-review.md` (final-postmortem
  step #4 + a Must-Check item). No extra loop iteration required.
- **Source:** RALPH run phase2-loop, after-action postmortem; diagnostics finding #5 +
  adversarial triage MR-1 (flagged independently by **both** review stages)
- **Issue:** With the review interval at 1, every iteration gets a mid-loop review *except
  the last one*: the loop ends after the final work commit, so it is never covered by a
  mid-loop review — only the after-action postmortem reviews it. This run, `2ba8f60`
  (iter-04 / C.3) was reviewed solely by the postmortem. No defect slipped through this
  time, but a future final-commit defect structurally could.
- **Decision needed:** Process call on how to guarantee final-commit coverage. Options
  for a human to choose: (a) codify in `ralph-output-adversarial-review.md` that the
  postmortem MUST deep-review the run's end commit (already done manually this run —
  cheapest, no extra loop iteration); (b) have the loop run one final mid-loop review at
  run end before exiting (`ralph-loop.md` Step 13); (c) accept the gap and rely on the
  postmortem's general coverage. All are additive and need maintainer sign-off before the
  skill files are edited.
- **Date parked:** 2026-06-17

### Idle-CPU assertion rests on a fragile process-wide gauge (measurement methodology)
- **⚠ MATERIALIZED 2026-06-17 on PR #3 CI** (exactly as predicted): `IdlePool_CpuUsage_IsNearZero`
  failed on both ubuntu-latest and windows-latest (process overhead ÷ few cores exceeds `< 0.20`).
  **Interim resolution (maintainer chose, option A):** scoped the two process-wide measurement smokes
  (idle-CPU + contention) `[Trait("Category","Measurement")]` and excluded them from CI
  (`TEST_FILTER='Category!=Measurement'`); they still run locally. **This PARK stays OPEN** for the
  robust methodology (option b — per-thread `ProcessThread.TotalProcessorTime` / calibrated threshold),
  to be decided when the **Phase 3 [GATED]** idle-CPU gate is wired.
- **Source:** RALPH run phase2-c4-fix, after-action adversarial review (postmortem), Finding #2
- **Issue:** `IdlePool_CpuUsage_IsNearZero` asserts `Environment.CpuUsage` (a **process-wide**
  gauge) is `< 0.20` of one core over a 2 s window. The C.4 `DisableParallelization` fix makes
  the gate pass deterministically on the dev box (i9-9900K/8c, 5/5 runs at ~14%), but the measured
  value swings on intra-process load alone — truly-isolated single test = **10.3%**, the measuring
  class alone (no siblings) = **14–16%**, a 2-test in-process filter reproduces the original
  **17.4%**. The ~5pp margin is set by runner/JIT/GC churn the test cannot control, not by pool
  behaviour, and there is no evidence it survives a smaller/loaded CI runner (2-core agent, same
  process-wide gauge ÷ fewer cores). The contention half (`Monitor.LockContentionCount` delta) is
  likewise process-wide and asserts nothing.
- **Decision needed:** Methodology call. Options: (a) keep the cheap process-wide gauge + the
  collection isolation already shipped (flaky-but-passing on this box; accept CI risk);
  (b) measure the **subject specifically** — sum the pool's own worker-thread CPU via per-thread
  `ProcessThread.TotalProcessorTime`, or calibrate the threshold against a same-process idle baseline
  rather than a hardcoded `<20%`; (c) lengthen the window / widen tolerance. This interacts with the
  **Phase 3 GATED idle-CPU gate** (`≈0` per `dae34f6d`), which will inherit this harness — so it
  should be decided before that gate is wired. Phase 3 is GATED, so this does not block the loop.
- **Date parked:** 2026-06-17

### 🔴 GATES PHASE 3 G8 — Pin / pre-warm .NET ThreadPool + re-run at a real BDN job before the Phase 3 acceptance benchmark
- **Source:** RALPH run phase3-prep, after-action adversarial review (postmortem), triage #5 (§B/§F)
  — see `.ralph/runs/phase3-prep/postmortem.md` (PARK-B)
- **Issue:** The P3.0.1 landscape rig (memorizer `1eeb3867`) is labelled PRELIMINARY and is honest
  about two methodology limits, but those limits make its headline numbers **unsafe to feed the gated
  Phase 3 G8 acceptance decision** as-is:
  - **Unconstrained TP at `WorkerCount=2`:** the `0.62` "Helios faster at 2w" ratio compares a 2-worker
    Helios against an **unconstrained ~8-worker, cold-ramping .NET ThreadPool** (the .NET TP can't be
    held to 2 threads without a global `SetMaxThreads`). Apples-to-oranges — must NOT be quoted as a
    parity finding.
  - **High error bars at ShortRun:** Error ≈ Mean for the .NET TP rows (e.g. Error 9.0ms on a 10.0ms
    Mean), so the `1.05` / `0.62` ratios are statistically soft.
- **Decision needed:** Before the **Phase 3 G8** parity/convergence gate consumes any of these numbers,
  re-run the comparison with (a) a pinned / pre-warmed .NET ThreadPool (e.g. `SetMinThreads`/`SetMaxThreads`
  + warm-up invocations so the TP isn't cold-ramping) so the WorkerCount config is a fair like-for-like
  comparison, and (b) a **real BDN job** (not ShortRun) so the error bars tighten per discipline `dae34f6d`.
  This is **NOT a NOW fix** — Phase 3 is `[GATED]` and human-gated, so the loop never reaches G8; but this
  is the item that unblocks an honest G8 acceptance decision and should be done as part of standing up that
  gate. **Prominence: this is the highest-priority park item — it gates the Phase 3 G8 acceptance benchmark.**
- **Date parked:** 2026-06-17

### iter-log has no `## Commits` section listing its hex hash (ralph-loop template gap)
- **Source:** RALPH run phase3-prep, after-action — flagged by **both** review stages (diagnostics
  finding G3/SP-2 + adversarial triage #4) — see `.ralph/runs/phase3-prep/postmortem.md` (PARK-A)
- **Issue:** `iter-01.md` has no `## Commits` section listing its actual commit hash (`bb63a41`), which
  violates the review skill's must-check (line 472: every iter log must list its hex commit hash). Because
  the log relied on git correlation instead, the STAGE 1 `gather-context` step had to perform an
  orphaned-commit hunt to map commits ↔ logs — and it surfaced the orphaned run-start commit `71a4122`
  (the plan-seeding commit recorded in `run.md` as the start commit but covered by no iteration log). This
  is a **2+-occurrence pattern**: both reviewers independently landed on the missing-commit-record gap in a
  single run.
- **Decision needed:** Process call (skill edit needs maintainer sign-off). The postmortem drafts a concrete
  fix: add a `## Commits` section **gate** to the `ralph-loop.md` iteration template (every `iter-NN.md` must
  list each commit's hex hash + one-line description) plus a `## Pre-run prep commits:` rule in `run.md` for
  any commit made before `iter-01`, and a Step-13 loop-advance gate that refuses to advance until the
  just-finished iter-log has a non-empty, git-resolvable `## Commits` section. All additive; needs sign-off
  before the skill files are edited.
- **Date parked:** 2026-06-17
