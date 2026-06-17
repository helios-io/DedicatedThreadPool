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
