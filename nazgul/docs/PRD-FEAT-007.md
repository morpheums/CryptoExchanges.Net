# PRD — FEAT-007: Rename DI Aggregator to Root Meta-Package `CryptoExchanges.Net`

- **Status**: Approved for implementation
- **Date**: 2026-06-21
- **Feature ID**: FEAT-007
- **Type**: Brownfield refactor — package identity rename + test decoupling, no behavior change

## Problem Statement

The package `CryptoExchanges.Net.DependencyInjection` is the all-exchanges meta-bundle: it
references all five exchange packages (Binance, Bybit, OKX, Bitget, KuCoin) plus Core and Http,
and exposes `AddCryptoExchanges()` as the one-call convenience registration. But per ADR-001, the
real per-exchange DI wiring already lives inside each exchange assembly. "DependencyInjection" no
longer names anything unique — every package has DI extensions — and the name hides the true
identity of this package: the bundle you install to get every exchange at once. A consumer browsing
NuGet cannot discover that `CryptoExchanges.Net.DependencyInjection` is the all-in-one entry point.

A secondary problem: the five per-exchange `.Tests.Unit` projects each reference the aggregator
solely to host a single `AddCryptoExchanges` resolution test. This drags every exchange into each
exchange's unit-test project, violating the isolation intent of ADR-001 and inflating build graphs
unnecessarily.

## Goals

1. Rename the aggregator package to the bare root id `CryptoExchanges.Net` — the honest name for
   "install this, get all exchanges."
2. Move `AddCryptoExchanges` and `CryptoExchangesOptions` to namespace `CryptoExchanges.Net`
   (method name and options shape unchanged).
3. Decouple the five per-exchange unit-test projects from the aggregator; consolidate the
   all-exchanges resolution test into a single test in the renamed `CryptoExchanges.Net.Tests.Unit`.
4. Repoint all legitimate consumers (MCP server, samples, sln) to `CryptoExchanges.Net`.
5. Publish 9 packages at version `0.5.0-preview.1` — `…DependencyInjection` out,
   `CryptoExchanges.Net` in — with docs and CHANGELOG updated.

## Out of Scope

- No transitional shim or type-forwarder: there are no existing consumers, making a clean swap safe.
- No change to per-exchange `AddXxxExchange` APIs, signing logic, mapping, or streaming.
- No new plugin or auto-discovery registration — the explicit meta-package referencing all exchanges
  is the correct design; only the package identity was wrong.
- The `AddCryptoExchanges` method name and `CryptoExchangesOptions` property shape are unchanged.
- Manual nuget.org deprecation/unlist of the old `…DependencyInjection` preview versions is a
  documented post-merge step, not part of this feature.

## User Stories

**As a NuGet consumer**, I run `dotnet add package CryptoExchanges.Net` and get all five exchange
clients with a single `services.AddCryptoExchanges(...)` call — the package name matches what it
does.

**As a per-exchange library consumer** (e.g. Binance-only), my `…Binance.Tests.Unit` project does
not reference the all-exchanges meta-package; my test build only compiles the exchange I care about.

**As an MCP server operator**, the host application references `CryptoExchanges.Net` (not the old
name); `AddCryptoExchanges()` wires all five exchanges as before.

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| AC-1 | `dotnet pack -c Release` produces 9 `.nupkg` including `CryptoExchanges.Net.0.5.0-preview.1.nupkg`; no package named `…DependencyInjection` in the output. |
| AC-2 | `AddCryptoExchanges()` resolves a working `IExchangeClient` for all five exchanges, verified by a single `AddCryptoExchanges_ResolvesAllFiveExchanges` test in `CryptoExchanges.Net.Tests.Unit`. |
| AC-3 | No project named or ID'd `CryptoExchanges.Net.DependencyInjection` remains in the solution. |
| AC-4 | Each per-exchange `.Tests.Unit` project has no ProjectReference to the aggregator and no `using CryptoExchanges.Net.DependencyInjection;`; all `AddXxxExchange` tests still pass. |
| AC-5 | `CryptoExchanges.Net.Mcp`, `CryptoExchanges.Net.Mcp.Tests.Unit`, and `samples/BasicUsage` reference `CryptoExchanges.Net`; MCP still resolves all exchanges. |
| AC-6 | `dotnet build CryptoExchanges.Net.sln` → 0 warnings, 0 errors (`TreatWarningsAsErrors`, `AnalysisLevel=latest-all`). |
| AC-7 | `dotnet test --filter 'Category!=Integration'` → all green; aggregator-resolution coverage exists exactly once (in `CryptoExchanges.Net.Tests.Unit`). |
| AC-8 | `README.md`, `NUGET_README.md`, `docs/` public files, and `CHANGELOG.md` reference `CryptoExchanges.Net`; no `…DependencyInjection` package name in consumer-facing text. |

## Constraints

- 4-layer chain (Core → Http → Exchange → DI/meta) preserved; meta-package may reference all
  exchange layers but nothing downstream adds a new transitive dependency.
- One-type-per-file rule applies to all moved/renamed source files.
- LEAN comments + LEAN XML docs: short `<summary>` + `<param>` per param + `<exception>` where
  thrown; `<inheritdoc/>` on implementations (the rule that bit FEAT-006 — enforce strictly).
- No opsec leakage in public artifacts (README, commits, PR, release notes).
