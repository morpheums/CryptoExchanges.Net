# API Review — TASK-008 (Bybit tests + AddBybitExchange DI, closes M-BYBIT)

**Reviewer**: API Reviewer
**Commit**: f60bd18
**Branch**: feat/m2-exchange-expansion
**Date**: 2026-06-17

---

## Findings

### Finding: AddBybitExchange signature shape
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:141-145`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:44-48` (AddBinanceExchange)

`AddBybitExchange(this IServiceCollection services, Action<BybitOptions>? configure = null)` is structurally identical to `AddBinanceExchange`: same extension-method shape, same nullable-optional `configure` parameter with `= null` default, same `IServiceCollection` return type, `ArgumentNullException.ThrowIfNull(services)` guard present, XML doc block present. Purely additive.

---

### Finding: CryptoExchangesOptions new members (BybitBaseUrl/BybitApiKey/BybitSecretKey)
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:280-287`
- **Category**: API Design | Compatibility
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:271-277` (Binance trio)

All three new properties are `string?` (nullable), have XML docs, and are purely additive on the `sealed class`. Existing callers compile unchanged.

---

### Finding: AddCryptoExchanges extension unchanged signature + additive body
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:237-261`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A

Method signature unchanged. Bybit block appended after existing Binance block using the same null-coalescing pattern.

---

### Finding: InternalsVisibleTo grants in CryptoExchanges.Net.Bybit.csproj
- **Severity**: MEDIUM
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:19-26`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj:18-21`

Four grants: `Tests.Unit`, `Tests.Integration`, `DynamicProxyGenAssembly2` (Castle/NSubstitute standard for mocking internal interfaces — not a first-party shipping package), and `CryptoExchanges.Net.DependencyInjection`. No consumer application assemblies granted access. `DynamicProxyGenAssembly2` is justified because Bybit unit tests mock the internal `IBybitHttpClient` interface via NSubstitute; the Binance csproj omits this grant only because Binance tests do not mock internal interfaces directly.

---

### Finding: Backwards compatibility
- **Severity**: HIGH
- **Confidence**: 100
- **File**: N/A (no Core files modified)
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A

No files under `src/CryptoExchanges.Net.Core/` were modified. `AddBinanceExchange` body untouched. No interface member added/removed/renamed, no enum values reordered/renamed, no model record properties altered. All changes are purely additive.

---

### Finding: NuGet package conventions
- **Severity**: MEDIUM
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:1-33`
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj`

`PackageId` and `Description` present. `PackageLicenseExpression=Apache-2.0` and `GenerateDocumentationFile=true` inherited from `Directory.Build.props`. Both test projects have `<IsPackable>false</IsPackable>`.

---

### Finding: BybitOptions and BybitExchangeClient pattern conformance
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs`, `src/CryptoExchanges.Net.Bybit/BybitExchangeClient.cs`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:12-28`

`BybitOptions` is a `sealed class` with `BaseUrl`, `ApiKey`, `SecretKey`, `TimeoutSeconds`, `ReceiveWindow` — matching `BinanceOptions` shape exactly with all XML docs. `BybitExchangeClient` is `public sealed class` implementing `IExchangeClient` and `IAsyncDisposable` with `static Create(BybitOptions)` and `static CreateFromEnvironment()`.

---

## Summary

- PASS: `AddBybitExchange` — identical shape to `AddBinanceExchange`, optional nullable configure, returns IServiceCollection, guard + XML doc present
- PASS: `CryptoExchangesOptions` Bybit members — naming/nullability/XML-doc consistent with Binance trio, purely additive
- PASS: `AddCryptoExchanges` — signature unchanged, Bybit block appended consistently
- PASS: `InternalsVisibleTo` grants — test-only + DynamicProxyGenAssembly2 (NSubstitute standard, no first-party shipping package), mirrors Binance pattern
- PASS: Backwards compatibility — no Core interfaces/models/enums modified, AddBinanceExchange body untouched, purely additive
- PASS: NuGet conventions — PackageId/Description present, license/doc inherited, test projects IsPackable=false
- PASS: BybitOptions/BybitExchangeClient — follow established Binance patterns exactly

---

## Final Verdict

**APPROVED**

All public API surface changes are purely additive. The `AddBybitExchange` DI method, `CryptoExchangesOptions` Bybit members, `InternalsVisibleTo` grants, and `BybitOptions`/`BybitExchangeClient` public entry class conform to the established Binance patterns with no deviations. No breaking changes to any existing public contract.
