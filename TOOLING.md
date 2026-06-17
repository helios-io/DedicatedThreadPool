# TOOLING.md

> Tooling and external-resource inventory for agents. Mutable. No secrets here —
> only what exists, how it is reached, and what it enables.
>
> Last reviewed: 2026-06-17

## Local CLIs (confirmed present)

| Tool | Version | Enables |
|---|---|---|
| `dotnet` | SDK 8.0.400, 9.0.311, 10.0.x | Build/test/pack. `net10.0` is the perf reference runtime. Pin via `global.json`. |
| `pwsh` | 7.6.3 | Runs the modern `build.ps1` (cross-platform). FAKE is being retired. |
| `git` | 2.43 | VCS. Default branch `dev`. Branch before committing on `dev`/`main`/`master`. |
| `gh` | 2.45 (authed: `Aaronontheweb`) | PRs, releases, Actions. **Issues are disabled** on this repo — use `gh pr` / `gh release`, not `gh issue`. |
| `ilspycmd` | 10.1 | Decompile assemblies to study runtime internals (e.g. `PortableThreadPool`, `LowLevelLifoSemaphore`, `HillClimbing.cs`). See dotnet-skills `ilspy-decompile`. |
| `dotnet-dump` | 9.0 | Post-mortem / hang analysis for deadlock and parking bugs. |
| `pbm` | 1.5 | Petabridge CLI (Akka.NET management) — not used by this primitive repo. |
| `rg` | 14.1 | Fast search. |

## Planned local tools (`.config/dotnet-tools.json`, to be added)

Modeled on `akkadotnet/build-system-template`:

- **Incrementalist** — change-detection to scope CI builds to affected projects.
- **docfx** — optional API docs site.
- Restored in CI/locally via `dotnet tool restore`.

## Build / test / benchmark stack (target state)

- **Build:** `build.ps1` (pwsh) orchestrates restore → build → test → pack. Central
  package management via `Directory.Packages.props`; shared props via
  `Directory.Build.props`; SDK pinned via `global.json`.
- **Tests:** **xUnit** (migrating from NUnit). Coverage via **coverlet**
  (`coverlet.runsettings`). Library targets `net10.0` only for now (netstandard2.0 deferred).
- **Benchmarks:** **BenchmarkDotNet** (replacing NBench). Diagnostics: `dotnet-trace`,
  `dotnet-counters`, `dotnet-dump`. Follow the benchmark discipline in memorizer
  `dae34f6d` before publishing any number.
- **CI/CD:** **GitHub Actions** (`.github/workflows/`) — PR validation (build + test
  on `net10.0`, Linux + Windows) and tag-driven NuGet release. Azure Pipelines YAML
  under `build-system/` is legacy and slated for removal.

## External resources

| Resource | Access | Enables |
|---|---|---|
| **memorizer** MCP server | `mcp__memorizer__*` tools (search/get) | Authoritative design spec, profiling evidence, benchmark discipline, rejected-lever history. **Source of truth** for pool/perf design. Start at `2c734cfb-0fb3-42e2-a855-fbea6a56ce83`. |
| **GitHub** | `gh` (authed) | PRs, Releases, Actions runs/logs. |
| **NuGet.org** | publish via CI on tag (API key as CI secret — never in repo) | Package distribution (content-only source package). |
| **.NET runtime source** | public (GitHub / `ilspycmd`) | Reference for `HillClimbing.cs`, `LowLevelLifoSemaphore`, Chase-Lev deque patterns. |

## Harness agents/skills available (by reference)

Use these rather than reimplementing expertise inline. Selection rules live in
`AGENTS.md`; the catalog:

- **Concurrency / correctness:** `dotnet-concurrency-specialist`,
  skill `analyze-racy-test`, dotnet-skills `csharp-concurrency-patterns`.
- **Performance:** `dotnet-benchmark-designer`, `dotnet-performance-analyst`,
  skills `create-benchmark` / `run-benchmark` / `benchmark-workflow`.
- **Code quality:** dotnet-skills `csharp-coding-standards`,
  `csharp-type-design-performance`, `csharp-api-design`; skills `code-review`,
  `simplify`, `slopwatch`, `crap-analysis`, `test-bloat-analysis`.
- **Build/release:** dotnet-skills `project-structure`, `package-management`,
  `local-tools`, `marketplace-publishing`; skills `create-release`,
  `check-api-breaking`, `commit`, `pr`.
- **Internals research:** dotnet-skills `ilspy-decompile`.
- **Generic:** `Explore` (read-only fan-out search), `Plan`, `general-purpose`,
  `fork` (inherits context).

## Recording baselines & measurements in memorizer (discipline)

When recording a benchmark or measurement baseline to memorizer:

- **Version, don't overwrite.** Edit the existing record (e.g. the baseline `4cedbe2f`) and add a
  new versioned section; preserve the prior reading with a context label (e.g. "v2 — contaminated")
  rather than deleting it. Discarding an inconvenient number to make a gate look clean is reward-hacking.
- **Label honestly.** A full-suite number is "full-suite," not "isolated." For process-wide
  measurements record the three-way decomposition (skill `de-flake-measurement-test`) and tag the
  figure that is *genuinely* isolated.
- **Keep the caveats.** Machine, load, governor, high-priority status, process-wide-vs-subject-specific,
  ShortRun-vs-full — all belong in the record. Preliminary ≠ official: the official baseline is
  `[GATED]` (cooled bare-metal, `governor=performance`, ≥3 reps per discipline `dae34f6d`).
- **Link it.** Relate baselines to the spec they support (`BASELINE-FOR` → `2c734cfb`).

## Assumptions / gaps

- NuGet publish API key and any signing secrets are **CI secrets**, not present
  locally; release flow assumes they are configured in GitHub Actions before first
  release. Confirm with the maintainer before cutting a release.
- No staging/integration environment — this is a library; "deploy" == NuGet publish.
