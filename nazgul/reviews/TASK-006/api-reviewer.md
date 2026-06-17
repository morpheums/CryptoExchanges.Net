# API Review: TASK-006 — Bybit Services + DeltaMapper Profiles + Composer + ExchangeClient

**Reviewer**: api-reviewer  
**Commit**: 057d6d2  
**Date**: 2026-06-17  
**Overall Verdict**: APPROVED  
**Overall Confidence**: 92

---

## Accessibility Audit

All six files were scanned for unintended public declarations. The policy requires that ONLY
`BybitExchangeClient` and `BybitOptions` be public among the new files.

**Result**: PASS — no public type leaks found.

| File | Type | Accessibility |
|------|------|--------------|
| `Services/BybitMarketDataService.cs` | All DTOs + service class | `internal sealed` |
| `Services/BybitTradingService.cs` | All DTOs + service class | `internal sealed` |
| `Services/BybitAccountService.cs` | All DTOs + service class | `internal sealed` |
| `Mapping/BybitMappingProfiles.cs` | `BybitResponseProfile` | `internal sealed` |
| `Internal/BybitClientComposer.cs` | Class = `internal static`; methods = `public static` | Effectively internal — C# reduces member accessibility to the containing type's. DI package accesses via `InternalsVisibleTo`. Identical pattern to `BinanceClientComposer`. |
| `BybitExchangeClient.cs` | Class | `public sealed` (correct) |
| `BybitExchangeClient.cs` | `BybitServerTimeResult` DTO | `internal sealed record` (correct) |

Note on `BybitClientComposer`: the `public static` members on an `internal static class`
are compiler-correct and functionally internal. The DI project (`CryptoExchanges.Net.DependencyInjection`)
accesses them via `InternalsVisibleTo`. This mirrors `BinanceClientComposer` exactly (line 13/16 of
`src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs`).

Note on `BybitResponseProfile`: the constructor is `public` on an `internal sealed class`, same pattern
as `BinanceResponseProfile` (`src/CryptoExchanges.Net.Binance/Mapping/BinanceMappingProfiles.cs:20`).
Not externally accessible.

---

## Findings

### Finding: GetOrderHistoryAsync and GetTradeHistoryAsync default limit=500 always throws
- **Severity**: HIGH
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Services/BybitTradingService.cs:198-205` and `src/CryptoExchanges.Net.Bybit/Services/BybitAccountService.cs:107-113`
- **Category**: API Design / Compatibility
- **Verdict**: REJECT (blocking — confidence 95)
- **Issue**: Both `GetOrderHistoryAsync` and `GetTradeHistoryAsync` declare `int limit = 500` (matching the interface default at `IExchangeClient.cs:111/138`) but `BybitRequestValidation.ValidateHistoryWindow` at line 25 throws `ArgumentOutOfRangeException` for any value outside `1..50`. Any caller using the default — including any caller who writes `client.Trading.GetOrderHistoryAsync(symbol)` — receives an unhandled exception with no warning from the signature. This breaks the Liskov Substitution Principle: polymorphic callers relying on `ITradingService.GetOrderHistoryAsync` with its documented default of 500 get an exception they cannot anticipate.
- **Fix**: Change the default parameter in both services to `int limit = BybitRequestValidation.MaxHistoryLimit` (i.e., 50). The interface allows implementations to cap the effective range; the method signature should reflect the safe upper bound for this exchange, not the interface's aspirational default. Alternatively, silently clamp the value: `limit = Math.Min(limit, MaxHistoryLimit)` instead of throwing. Clamping is preferable to an exception for a default-parameter regression. If documentation of the constraint is desired, add an XML `<remarks>` on the override noting the 50-record limit.
- **Pattern reference**: `IExchangeClient.cs:109-114` defines the interface default; any exchange-specific cap should use a safe default or clamp, not a hard throw on the most common call pattern.

---

### Finding: BybitOptions missing `<remarks>` on ReceiveWindow noting its type differs from TimeoutSeconds
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs:20-21`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 55)
- **Issue**: `ReceiveWindow` is typed `decimal` (milliseconds) while `TimeoutSeconds` is `int` (seconds). The existing summary says "milliseconds (decimal)" which is informative but a new user could confuse the two units. `BinanceOptions` has the same pattern (`BinanceExchangeClient.cs:24-27`) so this is parity, not a regression — just weak discoverability.
- **Fix**: Optionally add a `<remarks>` noting that `TimeoutSeconds` is the HTTP transport timeout, whereas `ReceiveWindow` is the Bybit `X-BAPI-RECV-WINDOW` header value sent to the exchange on every signed request.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:24-27` — same gap exists in Binance.

---

### Finding: BybitExchangeClient.Create() has a `<param>` doc that Binance's does not
- **Severity**: LOW
- **Confidence**: 50
- **File**: `src/CryptoExchanges.Net.Bybit/BybitExchangeClient.cs:65-67`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 50)
- **Issue**: `Create(BybitOptions options)` includes `/// <param name="options">The Bybit client options.</param>` while `BinanceExchangeClient.Create()` at `BinanceExchangeClient.cs:85-87` has no `<param>` doc. This is an improvement over Binance (more informative XML docs), not a defect, but it is a minor style divergence. Not a quality regression.
- **Fix**: None required; or, optionally backfill the `<param>` on `BinanceExchangeClient.Create` to match.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:85-87`

---

### Finding: NuGet project file missing PackageLicenseExpression
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj`
- **Category**: NuGet Conventions
- **Verdict**: CONCERN (non-blocking — confidence 70)
- **Issue**: `PackageLicenseExpression` is not declared in the `.csproj`. It is, however, inherited from `Directory.Build.props:12` (`Apache-2.0`). The review checklist asks whether new packages have `<PackageLicenseExpression>` — they do, via inheritance, not duplication. No actual gap; this is a checklist note.
- **Fix**: None required. The `Directory.Build.props` global inheritance is the correct pattern. The Binance `.csproj` likewise omits the explicit declaration.
- **Pattern reference**: `Directory.Build.props:12`; `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj` (also omits explicit declaration, relies on inheritance).

---

### Finding: BybitClientComposer InternalsVisibleTo granted to Tests.Integration (not Tests.Unit)
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 60)
- **Issue**: The `.csproj` grants `InternalsVisibleTo` to `CryptoExchanges.Net.Bybit.Tests.Integration`. TASK-008 calls for unit tests that mock `IBybitHttpClient`. If those unit tests live in a project named `CryptoExchanges.Net.Bybit.Tests` (without the `.Integration` suffix), they would not get visibility into internal DTOs/mapping profiles. Not an issue at this commit, but could cause a surprise when TASK-008 creates the test project.
- **Fix**: Confirm the TASK-008 test project name before wiring. If the unit test project is named differently from `CryptoExchanges.Net.Bybit.Tests.Integration`, add a second `InternalsVisibleTo` entry. The Binance `.csproj` grants the same `Tests.Integration` (which is the existing integration test project). Verify consistency.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj` line 13-17.

---

## Interface Compliance Check

All three services implement all required interface methods:

- `IMarketDataService` (8 methods): All present in `BybitMarketDataService` — GetTickersAsync, GetOrderBookAsync, GetCandlesticksAsync, GetPriceAsync, GetRecentTradesAsync, GetExchangeInfoAsync, IsSupportedAsync, ResolveSymbolAsync. PASS.
- `ITradingService` (7 methods): All present in `BybitTradingService` — PlaceOrderAsync, CancelOrderAsync, CancelOrderByClientIdAsync, CancelAllOrdersAsync, GetOrderAsync, GetOpenOrdersAsync, GetOrderHistoryAsync. PASS.
- `IAccountService` (3 methods): All present in `BybitAccountService` — GetBalancesAsync, GetBalanceAsync, GetTradeHistoryAsync. PASS.

All methods use `CancellationToken ct = default` as last parameter. PASS.
All collection methods return `Task<IReadOnlyList<T>>`. PASS.
All single-item methods return `Task<T>`. PASS.

## BybitExchangeClient Public Contract Check

| Required member | Present | Notes |
|----------------|---------|-------|
| `ExchangeId ExchangeId` | Yes | Returns `ExchangeId.Bybit` |
| `IMarketDataService MarketData` | Yes | |
| `ITradingService Trading` | Yes | |
| `IAccountService Account` | Yes | |
| `Task<bool> PingAsync(CancellationToken)` | Yes | Matches `IExchangeClient` |
| `static Create(BybitOptions)` | Yes | Mirrors `BinanceExchangeClient.Create` |
| `static CreateFromEnvironment()` | Yes | Reads `BYBIT_API_KEY`/`BYBIT_SECRET_KEY` |
| `Task SyncServerTimeAsync(CancellationToken)` | Yes | Correctly uses `_offsetHolder` |
| `IAsyncDisposable.DisposeAsync()` | Yes | Conditional on `_ownsHttpClient` |

`SyncServerTimeAsync` is not part of `IExchangeClient` — it is a Bybit-specific extra, same as in Binance. PASS.

## BybitOptions Public Contract Check

All required properties present with matching types vs `BinanceOptions`:

| Property | Type | Default |
|----------|------|---------|
| `BaseUrl` | `string` | `https://api.bybit.com` |
| `ApiKey` | `string` | `""` |
| `SecretKey` | `string` | `""` |
| `TimeoutSeconds` | `int` | `30` |
| `ReceiveWindow` | `decimal` | `5000m` |

PASS — parity with `BinanceOptions`.

## XML Documentation Coverage

CS1591 (missing XML docs warning) is suppressed in the `.csproj` via `<NoWarn>`. All **public** members on `BybitExchangeClient` and `BybitOptions` have XML summaries. `SyncServerTimeAsync` also has a `<param>` doc for `ct`. PASS.

Internal types with XML docs (beyond what is required): `BybitClientComposer` methods, `BybitResponseProfile`, key DTOs. This is good practice and consistent with the codebase standard.

## NuGet Package Conventions Check

- `PackageId`: `CryptoExchanges.Net.Bybit` — PASS
- `Description`: present — PASS
- `PackageLicenseExpression`: inherited from `Directory.Build.props` — PASS
- `IsPackable`: not false (library package, correct — should be packable) — PASS
- `GenerateDocumentationFile`: inherited from `Directory.Build.props:8` — PASS

---

## Summary

- PASS: Accessibility — all new internal types are `internal`; no DTO, service, or composer class leaked to public.
- PASS: BybitExchangeClient contract — all IExchangeClient members, factory methods (Create/CreateFromEnvironment/SyncServerTimeAsync), IAsyncDisposable implemented and XML-documented.
- PASS: BybitOptions contract — parity with BinanceOptions; all properties documented.
- PASS: Interface completeness — all 18 service interface methods implemented with correct return types and CancellationToken convention.
- PASS: DI InternalsVisibleTo — only test and DI packages granted access; no consumer app added.
- PASS: NuGet conventions — PackageId, Description, license all present (via inheritance).
- REJECT: `GetOrderHistoryAsync` and `GetTradeHistoryAsync` default `limit=500` clashes with `MaxHistoryLimit=50` validation — the default parameter on both methods should be `BybitRequestValidation.MaxHistoryLimit` (50) or the validation should clamp instead of throw. (confidence: 95, blocking)
- CONCERN: `BybitOptions` `ReceiveWindow` vs `TimeoutSeconds` unit mix could use a `<remarks>` clarification — minor, non-blocking (confidence: 55).
- CONCERN: `Create()` `<param>` doc is more informative than Binance's equivalent — positive divergence, not a defect (confidence: 50, non-blocking).
- CONCERN: `InternalsVisibleTo` targets `Tests.Integration` — verify the TASK-008 unit test project name matches before it fails to compile (confidence: 60, non-blocking, pre-emptive).

---

## Final Verdict

**CHANGES_REQUESTED**

One blocking issue: the `limit=500` default on `GetOrderHistoryAsync` and `GetTradeHistoryAsync` will throw `ArgumentOutOfRangeException` for every caller using the interface default. Fix by changing the default to `BybitRequestValidation.MaxHistoryLimit` (50) or clamping instead of throwing.

All other findings are non-blocking. The public API surface is clean, correctly scoped, and mirrors the Binance pattern faithfully.
