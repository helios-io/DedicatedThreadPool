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
