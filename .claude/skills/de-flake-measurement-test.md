---
name: de-flake-measurement-test
description: Fix flaky tests that assert on a process-wide measurement (Environment.CpuUsage, Monitor.LockContentionCount, GC counts, Process.TotalProcessorTime) under a parallel test runner. Use when a measurement/perf test passes in isolation but swings or fails under the full suite.
---

# De-flaking process-wide measurement tests

## Root-cause signature
A test asserts on a **process-wide** counter — `Environment.CpuUsage`, `Monitor.LockContentionCount`,
`GC.CollectionCount`, `Process.TotalProcessorTime`, etc. — over a time window, while the runner
executes other test classes **in parallel** (xUnit v2's default). Sibling tests (especially
`SpinWait`/`SpinUntil`/CPU-heavy ones) contaminate the process-wide sample, so the "idle"/"isolated"
reading swings run-to-run and the assertion is nondeterministic.

## Fix
Isolate the measurement from concurrent test execution:
- **xUnit v2:** put the measurement test(s) in a non-parallel collection —
  `[CollectionDefinition("Measurement", DisableParallelization = true)]` + `[Collection("Measurement")]`
  on the class. This serializes it relative to other collections.
- Or `xunit.runner.json` `"parallelizeTestCollections": false` (heavier — disables all parallelism).
- **Better where possible:** measure the **subject specifically** (per-thread
  `ProcessThread.TotalProcessorTime`, or a counter scoped to the object under test) instead of a
  whole-process gauge, so concurrent activity is irrelevant.

## Verification protocol (MANDATORY — the part that's easy to get wrong)
Before recording any number as "isolated," decompose the measurement **three ways** and run each ≥3×:
- **(a) single test alone** — filter to exactly one test, nothing else in process. This is the TRUE isolation floor.
- **(b) the measurement class alone** — zero sibling classes, but all its tests + listeners.
- **(c) the full suite** — with the parallelization fix in place.

Record the figure that matches **(a)** as the "isolated" number. If **(b) ≈ (c) ≫ (a)**, the residual is
**intra-process runtime churn** (JIT / GC / `Meter`/`MeterListener` / runner + OS scheduler threads),
**NOT** the parallel siblings — do not blame siblings. `DisableParallelization` removes *inter-test*
contention (makes the gate deterministic) but cannot remove the intra-process floor.

## Why this matters
Skipping the (b) decomposition is exactly how a mislabeled "isolated ~14%" (really the full-suite number)
with a wrong "SpinWait siblings caused it" root cause shipped into a baseline
(Helios.DedicatedThreadPool, run `phase2-c4-fix`, finding C4-1). The gate can be deterministic and still
ship a false explanation that downstream gates inherit.

## Caveats to record with the number
- Process-wide gauges have a thin, machine-dependent margin — note the box, its load, and that a
  smaller/loaded CI runner (fewer cores, same process-wide gauge ÷ fewer cores) may not preserve it.
- A process-wide counter asserts **nothing** about the subject specifically — say so explicitly.
- See `TOOLING.md` → "Recording baselines & measurements in memorizer" for how to version the record.
