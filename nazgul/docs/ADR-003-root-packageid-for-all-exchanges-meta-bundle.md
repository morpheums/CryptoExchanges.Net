# ADR-003: Bare root id `CryptoExchanges.Net` as the all-exchanges meta-package

- **Status**: Accepted
- **Date**: 2026-06-21
- **Context**: FEAT-007 (aggregator rename). Follows ADR-001 (per-exchange DI) and ADR-002
  (streaming seam). Numbered sequentially.

## Context

Per ADR-001, each exchange ships its own `AddXxxExchange` extension inside its own assembly.
The existing `CryptoExchanges.Net.DependencyInjection` package merely aggregates those calls:
it references all five exchange packages and delegates to their `AddXxxExchange` methods via
`AddCryptoExchanges()`. The name "DependencyInjection" no longer describes anything unique
(every package has DI extensions) and actively obscures the package's identity as the
all-exchanges meta-bundle.

Two concrete problems:

1. **Discoverability**: a NuGet consumer searching for the one package that wires all exchanges
   cannot identify it from the name `CryptoExchanges.Net.DependencyInjection`.
2. **Test coupling**: 4 of the 5 per-exchange `.Tests.Unit` projects reference the aggregator
   solely to host a local `AddCryptoExchanges` resolution test, transitively pulling every
   exchange into each exchange's isolated unit-test build.

The repository currently has 9 published packages; all carry a suffix (`.Core`, `.Http`,
`.Binance`, `.Mcp`, etc.). The bare root id `CryptoExchanges.Net` is unused and available.

## Decision

Claim the bare root id `CryptoExchanges.Net` as the all-exchanges meta-package, replacing
`CryptoExchanges.Net.DependencyInjection`. Move `AddCryptoExchanges` and `CryptoExchangesOptions`
to the `CryptoExchanges.Net` namespace (method name and options shape unchanged). The published
set stays at 9; `…DependencyInjection` is removed from the build and deprecated on nuget.org
(manual post-merge step). No transitional shim: there are no existing consumers.

The per-exchange test coupling is resolved in the same change: remove aggregator references from
the 4 coupled per-exchange test projects, delete the moved tests there, and consolidate into a
single `AddCryptoExchanges_ResolvesAllFiveExchanges` test in the renamed
`CryptoExchanges.Net.Tests.Unit`.

## Consequences

- **NuGet identity**: the meta-package is now `CryptoExchanges.Net` — the name that matches the
  repository, the library, and the concept.
- **Namespace consistency**: per-exchange extensions live in `CryptoExchanges.Net.Binance`,
  `CryptoExchanges.Net.Bybit`, etc.; the aggregator now lives in `CryptoExchanges.Net`. All
  follow the pattern `using CryptoExchanges.Net[.ExchangeName];`.
- **Test isolation restored**: each per-exchange `.Tests.Unit` project depends only on its own
  exchange package plus Core/Http/test libs. The all-exchanges resolution test lives exactly once.
- **No runtime change**: `AddCryptoExchanges()` method body is identical; the handler chain,
  signing, mapping, and streaming are completely untouched.
- **Breaking change scope**: package id and `using` directive only. Because there are no released
  non-preview consumers, the migration is a clean swap. CHANGELOG documents the two-line migration.

## Alternatives Considered

### A — Keep `…DependencyInjection`; add an alias package `CryptoExchanges.Net`

Would require maintaining two `.nupkg` files that both depend on the aggregator, or making one a
thin wrapper. This duplicates metadata and keeps the confusing original name alive. Rejected:
complexity with no benefit when there are no consumers to protect.

### B — Make each exchange package transitively pull in all others (no meta-package)

Removes the explicit aggregator but forces consumers who want only Binance to also compile Bybit,
OKX, Bitget, and KuCoin. Violates the selective-install principle and ADR-001's direction that
each exchange package is independently installable. Rejected: opposite of the intended design.

### C — Name the meta-package `CryptoExchanges.Net.All`

Adds a suffix that slightly improves discoverability versus `…DependencyInjection` but is still
non-canonical. The pattern `SomeLib.All` is common but `SomeLib` as the root name is cleaner
and more conventional (mirrors `Microsoft.Extensions.Hosting` vs individual sub-packages).
Rejected: less discoverable than the bare root id.

### D — Merge the aggregator into Core

Core carries no exchange-specific code (its value is zero transitive dependencies). Merging the
aggregator into Core would drag all five exchange assemblies into every Core consumer, collapsing
the 4-layer chain. Rejected: violates the foundational architectural constraint.

## Prior Documentation

- `nazgul/archive/2026-06-19-033855-FEAT-001-M2/docs/ADR-001-per-exchange-di-and-conventions.md` —
  establishes per-exchange DI wiring; this ADR's decision is consistent with it.
- `nazgul/docs/ADR-002-streaming-async-endpoint-seam.md` — preceding ADR (numbered 002).
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` — current
  `AddCryptoExchanges` implementation; namespace declaration is the only source change.
- `src/CryptoExchanges.Net.DependencyInjection/CryptoExchangesOptions.cs` — current options type;
  namespace declaration is the only source change.
- `tests/CryptoExchanges.Net.{Bybit,Okx,Bitget,Kucoin}.Tests.Unit/*.csproj` — the four test
  projects being decoupled; each currently holds a ProjectReference to the aggregator.
