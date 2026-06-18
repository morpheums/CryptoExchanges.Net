# API Reviewer — TASK-022: Bitget FINAL milestone closer

**Date**: 2026-06-18  
**Branch**: feat/m4-bitget  
**Commit**: a9caa69623fae9f88276e7bbdac2f9fd447b0e6d

---

## Final Verdict: APPROVED
**Confidence**: 95/100

No blocking findings. The Bitget public API surface is clean, all implementation types are correctly scoped `internal`, the `BitgetExchangeClient`/`BitgetOptions` pattern mirrors OKX exactly, the DI aggregator extension is additive and non-breaking, and NuGet/package conventions match the sibling OKX assembly.

---

## Blocking Findings (REJECT)

None.

---

## Non-blocking Concerns (CONCERN)

### Concern 1: `BitgetClientComposer` methods carry `public` access modifier inside an `internal` class
- **Severity**: LOW
- **Confidence**: 55/100
- **File**: `src/CryptoExchanges.Net.Bitget/Internal/BitgetClientComposer.cs:19-113`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking)
- **Issue**: All six static methods in `BitgetClientComposer` are declared `public static` despite the class being `internal static`. In C#, `public` members of an `internal` class are not accessible outside the assembly — this is functionally correct and will not leak to external consumers. However, it is stylistically inconsistent: the sibling OKX composer (pattern reference) follows the same convention, so this is an inherited pattern. Low concern because it matches the existing codebase style exactly.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs` — same `public static` on internal class

### Concern 2: `BitgetOptions` does not expose a `ReceiveWindow` property unlike `BinanceOptions`
- **Severity**: LOW
- **Confidence**: 60/100
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `BinanceOptions` exposes `ReceiveWindow` (Binance-specific; Bitget uses a different mechanism for request timestamp validation). Bitget V2 does not use a `recvWindow` parameter, so omitting it is intentional and correct. However, because the review instruction lists `BinanceOptions` as the minimum pattern (`BaseUrl`, `ApiKey`, `SecretKey`, `TimeoutSeconds` minimum), and `BitgetOptions` includes all four plus `Passphrase`, this is a PASS for the minimum requirement. Called out only for completeness.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:12-27`

### Concern 3: `CryptoExchanges.Net.Bitget.csproj` does not explicitly set `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>`
- **Severity**: LOW
- **Confidence**: 50/100
- **File**: `src/CryptoExchanges.Net.Bitget/CryptoExchanges.Net.Bitget.csproj:1-31`
- **Category**: NuGet Conventions
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The review checklist requires `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` on new source projects. The Bitget csproj omits it, relying on `Directory.Build.props` to provide the value. Checking `Directory.Build.props` confirms `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` IS present and inherited (line 12). The sibling OKX csproj also omits the explicit property for the same reason. This is consistent with the existing pattern — the field is inherited, not missing. Confidence is low (50) because the inheritance chain makes this correct.
- **Pattern reference**: `Directory.Build.props:12` — `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` inherited by all projects

---

## Checklist Results

### Breaking change detection
- PASS: No interface in `src/CryptoExchanges.Net.Core/Interfaces/` was modified by this diff.
- PASS: No `Models/Models.cs` record was structurally changed.
- PASS: No method signatures on any existing public type were changed.
- PASS: `ExchangeId` enum was not modified (Bitget value was pre-existing from prior tasks).
- PASS: `BinanceOptions` properties untouched.
- PASS: `AddBinanceExchange` / `AddBybitExchange` / `AddOkxExchange` signatures untouched.

### CryptoExchangesOptions additions (additive, non-breaking)
- PASS: Four new nullable properties added to `CryptoExchangesOptions`: `BitgetBaseUrl`, `BitgetApiKey`, `BitgetSecretKey`, `BitgetPassphrase`. All nullable (`string?`). No existing property modified. Consistent with `OkxPassphrase` pattern at line 101.
- PASS: `AddCryptoExchanges` calls `AddBitgetExchange` — additive DI registration, mirrors OKX pattern exactly.

### Public surface of the Bitget assembly
- PASS: Only `BitgetExchangeClient` (public sealed class) and `BitgetOptions` (public sealed class) and `ServiceCollectionExtensions` (public static class, public-by-necessity for DI) are `public` in the Bitget assembly.
- PASS: All internal types are correctly scoped: `BitgetSignatureService`, `BitgetSigningHandler`, `BitgetSigningRequest`, `IBitgetHttpClient`, `BitgetHttpClient`, `BitgetClientComposer`, `BitgetRequestValidation`, `BitgetValueParsers`, `BitgetResponseProfile`, `BitgetErrorTranslator`, `BitgetSymbolFormat`, `BitgetServerTime` (internal record in BitgetExchangeClient.cs), all DTOs (`BitgetResponse<T>`, `BitgetObjectResponse<T>`, `BitgetTicker`, `BitgetOrderBook`, `BitgetTrade`, `BitgetSymbol`, `BitgetBalance`, `BitgetFill`, `BitgetOrderAck`, `BitgetOrder`) — all `internal sealed`.
- PASS: No unintended public types found. Grep of all `public` declarations confirms only the three intended public types.

### BitgetExchangeClient mirrors OkxExchangeClient
- PASS: `static Create(BitgetOptions)` — present, delegates to `BitgetClientComposer.Create`.
- PASS: `static CreateFromEnvironment()` — present, reads `BITGET_API_KEY` / `BITGET_SECRET_KEY` / `BITGET_PASSPHRASE`. Env var names match the `BITGET_` prefix convention.
- PASS: `async Task SyncServerTimeAsync(CancellationToken ct = default)` — present, calls `/api/v2/public/time`, applies offset.
- PASS: `async Task<bool> PingAsync(CancellationToken ct = default)` — present (inherited from `IExchangeClient`), same catch pattern as OKX.
- PASS: `ExchangeId` returns `ExchangeId.Bitget` — correct.
- PASS: Implements `IExchangeClient` and `IAsyncDisposable` — both on the class declaration.
- PASS: `DisposeAsync()` — correctly gates disposal on `_ownsHttpClient`. Mirrors OKX.
- PASS: Internal constructor takes same parameter set as OKX (`IBitgetHttpClient`, `IMarketDataService`, `ITradingService`, `IAccountService`, `bool ownsHttpClient`, `HttpClient?`, `long[]`, `IExchangeTimeSync`).

### BitgetOptions mirrors OkxOptions
- PASS: `BaseUrl` (string, default `"https://api.bitget.com"`) — present.
- PASS: `ApiKey` (string, default `string.Empty`) — present.
- PASS: `SecretKey` (string, default `string.Empty`) — present.
- PASS: `TimeoutSeconds` (int, default 30) — present.
- PASS: `Passphrase` (string, default `string.Empty`) — present (required for Bitget, mirrors OKX).
- PASS: `ToCredentials()` — present; same signature as `OkxOptions.ToCredentials()`.

### AddBitgetExchange DI pattern
- PASS: Method signature: `public static IServiceCollection AddBitgetExchange(this IServiceCollection services, Action<BitgetOptions>? configure = null)` — matches OKX DI pattern exactly.
- PASS: Delegates to `ExchangeServiceRegistration.AddExchange<BitgetOptions, IMapper>` — same shared helper as OKX.
- PASS: `applyEnvDefaults` reads `BITGET_API_KEY`, `BITGET_SECRET_KEY`, `BITGET_PASSPHRASE`.
- PASS: `baseUrlSelector` calls `BitgetClientComposer.NormalizeHostRoot(o.BaseUrl)` — host-root guard active in DI path.
- PASS: Finalizer: `PassThroughHandler` when `SecretKey` OR `Passphrase` is empty; `BitgetSigningHandler` otherwise. Correctly avoids `ToCredentials()` (which throws on empty passphrase).
- PASS: `exchangeClientFactory` calls `BitgetClientComposer.ComposeForDi` — correct DI composition path.

### InternalsVisibleTo
- PASS: `InternalsVisibleTo` added to `CryptoExchanges.Net.Http.csproj` for `CryptoExchanges.Net.Bitget` — identical pattern to Binance/Bybit/OKX.
- PASS: `CryptoExchanges.Net.Bitget.csproj` grants visibility to `CryptoExchanges.Net.Bitget.Tests.Unit`, `CryptoExchanges.Net.Bitget.Tests.Integration`, and `DynamicProxyGenAssembly2` (NSubstitute) — consistent with OKX.
- PASS: No consumer application projects are granted InternalsVisibleTo.

### NuGet package conventions
- PASS: `<IsPackable>false</IsPackable>` set in both test projects.
- PASS: `<IsTestProject>true</IsTestProject>` set in both test projects.
- PASS: `<PackageId>CryptoExchanges.Net.Bitget</PackageId>` present.
- PASS: `<Description>Bitget exchange implementation for CryptoExchanges.Net.</Description>` present.
- PASS: `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` inherited from `Directory.Build.props`.
- PASS: `<GenerateDocumentationFile>true</GenerateDocumentationFile>` inherited from `Directory.Build.props:8`.

### API design quality (CancellationToken, return types, XML docs)
- PASS: All service methods accept `CancellationToken ct = default` as the last parameter.
- PASS: Collection-returning methods return `Task<IReadOnlyList<T>>`.
- PASS: Single-item methods return `Task<T>`.
- PASS: `BitgetExchangeClient` public methods have XML `<summary>` docs; inherited interface members use `<inheritdoc />`.
- PASS: `BitgetOptions` all public properties have XML `<summary>` docs.
- PASS: `AddBitgetExchange` has full XML `<param>`, `<summary>`, `<returns>` documentation.

---

## Summary

- PASS: Public/internal boundary — only `BitgetExchangeClient`, `BitgetOptions`, `ServiceCollectionExtensions` are public; all 25+ implementation types are correctly `internal sealed`.
- PASS: `BitgetExchangeClient` shape — exact mirror of `OkxExchangeClient` (Create/CreateFromEnvironment/SyncServerTimeAsync/PingAsync/ExchangeId/IAsyncDisposable).
- PASS: `BitgetOptions` shape — exact mirror of `OkxOptions` (BaseUrl/ApiKey/SecretKey/Passphrase/TimeoutSeconds/ToCredentials).
- PASS: `AddBitgetExchange` DI entry-point — exact mirror of `AddOkxExchange` pattern, passphrase-gated finalizer.
- PASS: `CryptoExchangesOptions` additions — four nullable string properties, consistent naming, no existing property modified.
- PASS: `AddCryptoExchanges` extension — additive `AddBitgetExchange` call, same delegation pattern.
- PASS: NuGet conventions — `IsPackable=false` on tests, `PackageId`/`Description` present, license inherited.
- PASS: InternalsVisibleTo — test assemblies + DynamicProxy only; no consumer apps.
- CONCERN: `public static` methods on `internal static class BitgetClientComposer` — stylistically inherited from OKX pattern; functionally correct; non-blocking (confidence: 55/100).
- CONCERN: No `ReceiveWindow` on `BitgetOptions` — intentional, Bitget V2 has no recvWindow; non-blocking (confidence: 60/100).
- CONCERN: `PackageLicenseExpression` not in Bitget csproj directly — inherited from `Directory.Build.props`, consistent with OKX; non-blocking (confidence: 50/100).
