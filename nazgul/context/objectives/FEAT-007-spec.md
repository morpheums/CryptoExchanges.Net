# FEAT-007 — Rename DI aggregator → root meta-package `CryptoExchanges.Net`

## Objective
Rename the all-exchanges DI aggregator from `CryptoExchanges.Net.DependencyInjection` to the bare
root id `CryptoExchanges.Net`, so the "install one package → get all exchanges + one-call
`AddCryptoExchanges()`" bundle is honestly named and discoverable. Move `AddCryptoExchanges` +
`CryptoExchangesOptions` to the `CryptoExchanges.Net` namespace (method name and options shape
unchanged). Decouple the five per-exchange unit-test projects from the aggregator. No runtime
behavior change.

## Objective type
Refactor (brownfield package rename + test decoupling). Public surface changes only in package id
and namespace.

## Authoritative design (read fully before planning)
- `docs/superpowers/specs/2026-06-21-rename-di-aggregator-root-metapackage-design.md` (PRIMARY —
  the approved brainstorming design; local/untracked).

## Purpose / problem
Per ADR-001, per-exchange DI wiring (`AddBinanceExchange`, …) already lives inside each exchange
assembly, so "DependencyInjection" no longer names anything unique — it hides this package's true
identity as the **all-exchanges meta-bundle** (it references all 5 exchanges + Core + Http). Second
smell: the 5 per-exchange `.Tests.Unit` projects reference the aggregator solely to host one
`Di_AddCryptoExchanges_Resolves…` test each, transitively dragging every exchange into each
exchange's unit-test project.

## Scope — In
1. **Rename project** `src/CryptoExchanges.Net.DependencyInjection/` → `src/CryptoExchanges.Net/`;
   csproj `PackageId`=`AssemblyName`=`RootNamespace`=`CryptoExchanges.Net`; keep refs to all 5
   exchanges + Core + Http; packaging metadata consistent with the other packable libs. Move
   `ServiceCollectionExtensions.cs` + `CryptoExchangesOptions.cs` to namespace `CryptoExchanges.Net`.
2. **Rename + consolidate test project** `…DependencyInjection.Tests.Unit` →
   `CryptoExchanges.Net.Tests.Unit`; replace the scattered per-exchange aggregator-resolution tests
   with one `AddCryptoExchanges_ResolvesAllFiveExchanges` (Binance/Bybit/OKX/Bitget/KuCoin), keeping
   existing `DiRegistrationTests`/`ExchangeClientFactoryTests`.
3. **Decouple** the 5 per-exchange `.Tests.Unit` projects: remove the aggregator ProjectReference +
   `using CryptoExchanges.Net.DependencyInjection;` and their local `Di_AddCryptoExchanges_*` tests
   (now covered once in #2). Keep all `AddXxxExchange()` tests (those use the exchange's own package).
   Result: each exchange unit-test project depends only on its own package + Core/Http/test libs.
4. **Repoint** legitimate consumers to `CryptoExchanges.Net`: MCP (`src/.../Mcp/Program.cs`,
   `EnvCredentialBinder.cs` + csproj), `Mcp.Tests.Unit`, `samples/BasicUsage`, and
   `CryptoExchanges.Net.sln`.
5. **Docs**: `README.md`, `NUGET_README.md`, `docs/architecture.md`, `docs/getting-started.md`,
   `docs/exchanges.md` → reference `CryptoExchanges.Net`; show `dotnet add package CryptoExchanges.Net`
   as the all-exchanges entry point. `CHANGELOG.md` 0.5.0-preview.1 entry + migration note.
6. **Version**: `Directory.Build.props` → `0.5.0-preview.1` (centralized, lockstep). Published set
   stays 9 (DI out, `CryptoExchanges.Net` in). `release.yml` unchanged (packs by solution glob).

## Scope — Out
- No transitional shim / type-forwarder (no consumers yet — clean swap).
- No change to per-exchange `AddXxxExchange` APIs, signing, mapping, or streaming.
- No plugin/auto-discovery registration — the explicit meta-package referencing all exchanges is
  the intended design; only its name was wrong.
- `AddCryptoExchanges` method name and `CryptoExchangesOptions` shape unchanged (namespace only).
- The actual nuget.org deprecation/unlist of old `…DependencyInjection` versions is a manual
  post-merge step (documented), not part of the build.

## Constraints
- 4-layer chain preserved; one-type-per-file; lean comments + lean XML docs (short `<summary>` +
  `<param>` per param + `<exception>`; `<inheritdoc/>` on impls — per the house rule that bit
  FEAT-006); `dotnet build CryptoExchanges.Net.sln` 0W/0E (TreatWarningsAsErrors,
  AnalysisLevel=latest-all). No opsec leakage in public artifacts.

## Success criteria
- `CryptoExchanges.Net` builds + packs; `AddCryptoExchanges()` resolves a working `IExchangeClient`
  for all five exchanges (one consolidated test).
- No project named/ID'd `CryptoExchanges.Net.DependencyInjection` remains in the solution.
- Each per-exchange `.Tests.Unit` has NO aggregator reference / `using`; its `AddXxxExchange` tests
  still pass.
- MCP + samples + sln reference `CryptoExchanges.Net`; MCP still resolves all exchanges.
- Build 0W/0E; non-integration suite green; aggregator-resolution coverage exists exactly once.
- `dotnet pack -c Release` → 9 `.nupkg` incl. `CryptoExchanges.Net.0.5.0-preview.1.nupkg`, none
  named `…DependencyInjection`. Docs + CHANGELOG updated.

## Build approach
Rename project → rename+consolidate aggregator test project → decouple the 5 per-exchange test
projects → repoint MCP/samples/sln → docs + CHANGELOG + version bump → build 0W/0E + suite green +
pack verifies the 9-package swap.
