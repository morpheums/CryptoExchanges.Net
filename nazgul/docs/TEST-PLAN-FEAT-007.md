# Test Plan — FEAT-007: Rename DI Aggregator to Root Meta-Package `CryptoExchanges.Net`

- **Status**: Approved for implementation
- **Date**: 2026-06-21
- **Feature ID**: FEAT-007
- **Test command (non-integration)**: `dotnet test --filter 'Category!=Integration'`

## Strategy

This is a refactor with no runtime behavior change. The testing objectives are:

1. **Verification**: the renamed package resolves all five exchanges correctly (consolidated test).
2. **Decoupling validation**: per-exchange test projects build and pass with no aggregator reference.
3. **Regression**: every existing unit test passes unchanged; no test is lost — only relocated.
4. **Pack verification**: `dotnet pack -c Release` produces exactly 9 `.nupkg`, including
   `CryptoExchanges.Net.0.5.0-preview.1.nupkg`, and none named `…DependencyInjection`.

No new network-touching tests are needed. All verification is done in the unit layer.

---

## Test Project Changes

| Project | Action | Reason |
|---------|--------|--------|
| `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/` | Rename → `CryptoExchanges.Net.Tests.Unit/`; update namespaces + usings; add `AddCryptoExchangesTests.cs` | Tracks the renamed package; consolidates scattered resolution tests |
| `tests/CryptoExchanges.Net.Bybit.Tests.Unit/` | Remove aggregator ProjectReference; remove `Di_AddCryptoExchanges_ResolvesBybitAndBinance` | Decoupling; test now lives in aggregator test project |
| `tests/CryptoExchanges.Net.Okx.Tests.Unit/` | Remove aggregator ProjectReference; remove `Di_AddCryptoExchanges_ResolvesOkxBybitAndBinance` | Decoupling |
| `tests/CryptoExchanges.Net.Bitget.Tests.Unit/` | Remove aggregator ProjectReference; remove `Di_AddCryptoExchanges_ResolvesBitgetOkxBybitAndBinance` | Decoupling |
| `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/` | Remove aggregator ProjectReference; remove `AddCryptoExchanges_ResolvesKucoinClient`, `AddCryptoExchanges_ResolvesAllFiveExchanges`, `AddCryptoExchanges_KucoinOptions_AppliesViaAggregator` from `KucoinDiTests.cs` | Decoupling; superseded by consolidated test |
| `tests/CryptoExchanges.Net.Binance.Tests.Unit/` | No change | Already has no aggregator reference |
| `tests/CryptoExchanges.Net.Mcp.Tests.Unit/` | Update `using`; update ProjectReference path | Consumer repoint only |
| All other test projects | No change | Unaffected |

---

## New and Modified Tests in `CryptoExchanges.Net.Tests.Unit`

### `AddCryptoExchangesTests.cs` (new file)

This file is the single authoritative location for all-exchanges aggregator resolution coverage.

| Test | Verification |
|------|-------------|
| `AddCryptoExchanges_ResolvesAllFiveExchanges` | `services.AddCryptoExchanges()` → service provider resolves `IExchangeClient` keyed by each of `ExchangeId.Binance`, `ExchangeId.Bybit`, `ExchangeId.Okx`, `ExchangeId.Bitget`, `ExchangeId.Kucoin`; each returns the correct `ExchangeId`. This is the one test that exercises the meta-package end-to-end. |
| `AddCryptoExchanges_OptionsFlow_ReachesExchangeOptions` | Configure delegate on `CryptoExchangesOptions` propagates (e.g. `BinanceApiKey`) to the resolved `BinanceOptions`; verifies the delegation chain in `ServiceCollectionExtensions`. |

### `DiRegistrationTests.cs` (modified — namespace + using only)

All existing tests preserved:

| Test | Preservation action |
|------|-------------------|
| `Resolves_KeyedExchangeClient` | Keep; `using CryptoExchanges.Net;` replaces old using |
| `Resolves_Mapper_AsSingleton` | Keep |
| `NoUnkeyed_ExchangeClient_Registered` | Keep |
| `InvalidOptions_FailFast_OnValidateOnStart` | Keep |
| `BybitOnly_Registration_ResolvesBybitClient` | Keep (demonstrates ADR-001 independence) |
| `Registers_ExchangeTimeSync_AsDefault` | Keep |
| `Consumer_Can_Override_ExchangeTimeSync` | Keep |
| `Registration_IsScopeClean` | Keep |

### `ExchangeClientFactoryTests.cs` (modified — namespace + using only)

All existing tests preserved:

| Test | Preservation action |
|------|-------------------|
| `Get_ReturnsRegisteredClient` | Keep |
| `Get_Unregistered_Throws` | Keep |
| `TryGet_Registered_ReturnsTrue` | Keep |
| `TryGet_Unregistered_ReturnsFalse` | Keep |
| `Available_ListsRegisteredExchanges` | Keep |

---

## Regression Coverage

### Per-exchange unit test suites after decoupling

Each decoupled test project must pass with no aggregator reference present.

| Project | Tests retained | Tests removed |
|---------|---------------|--------------|
| `CryptoExchanges.Net.Bybit.Tests.Unit` | All `AddBybitExchange*`, mapping, service tests | `Di_AddCryptoExchanges_ResolvesBybitAndBinance` |
| `CryptoExchanges.Net.Okx.Tests.Unit` | All `Di_AddOkxExchange_*`, mapping, service tests | `Di_AddCryptoExchanges_ResolvesOkxBybitAndBinance` |
| `CryptoExchanges.Net.Bitget.Tests.Unit` | All `Di_AddBitgetExchange_*`, mapping, service tests | `Di_AddCryptoExchanges_ResolvesBitgetOkxBybitAndBinance` |
| `CryptoExchanges.Net.Kucoin.Tests.Unit` | All `AddKucoinExchange*`, signing, mapping, streaming tests | `AddCryptoExchanges_ResolvesKucoinClient`, `AddCryptoExchanges_ResolvesAllFiveExchanges`, `AddCryptoExchanges_KucoinOptions_AppliesViaAggregator` |
| `CryptoExchanges.Net.Binance.Tests.Unit` | All streaming unit tests (no change needed) | — |

### MCP tests

`tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs` — `using` update only;
`CryptoExchangesOptions` type is still resolvable from `CryptoExchanges.Net`; all existing
assertions must pass unchanged.

### Unaffected suites

`CryptoExchanges.Net.Core.Tests.Unit`, `CryptoExchanges.Net.Http.Tests.Unit`,
`CryptoExchanges.Net.Binance.Tests.Integration`, `CryptoExchanges.Net.Kucoin.Tests.Integration`
— no changes; must pass without any intervention.

---

## Pack Verification

After `dotnet pack -c Release`, assert:

| Check | Expected |
|-------|---------|
| Package count | Exactly 9 `.nupkg` files |
| New package present | `CryptoExchanges.Net.0.5.0-preview.1.nupkg` |
| Old package absent | No file matching `*DependencyInjection*.nupkg` |
| All other packages | `CryptoExchanges.Net.Core.0.5.0-preview.1.nupkg`, `CryptoExchanges.Net.Http.*`, `CryptoExchanges.Net.Binance.*`, `CryptoExchanges.Net.Bybit.*`, `CryptoExchanges.Net.Okx.*`, `CryptoExchanges.Net.Bitget.*`, `CryptoExchanges.Net.Kucoin.*`, `CryptoExchanges.Net.Mcp.*` |

---

## Definition of Done for Tests

- `dotnet test --filter 'Category!=Integration'` → 0 failures, 0 skips on non-integration tests.
- Aggregator-resolution coverage exists exactly once: in `CryptoExchanges.Net.Tests.Unit`.
- No per-exchange `.Tests.Unit` project references `CryptoExchanges.Net.DependencyInjection`
  (or any path containing `DependencyInjection`).
- `dotnet build CryptoExchanges.Net.sln` → 0 warnings, 0 errors.
- `dotnet pack -c Release` → 9 packages; no `…DependencyInjection` package.
