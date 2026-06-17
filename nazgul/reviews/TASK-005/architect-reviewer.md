# Architect Review: TASK-005 — BybitHttpClient + IBybitHttpClient

**Reviewer**: Architect Reviewer
**Commit**: 2a598c8
**Branch**: feat/m2-exchange-expansion
**Files reviewed**:
- `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs`
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs`

---

## Findings

### Finding 1: Layering and Dependency Direction
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:1-5`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:1-8`

The csproj references only Core and Http (`CryptoExchanges.Net.Bybit.csproj:12-14`). The two reviewed files carry no `using` directives that cross into Core, Http, DI, or any sibling exchange. The only non-BCL `using` is `CryptoExchanges.Net.Bybit.Resilience`, which is intra-project. The dependency chain (Core → Http → Bybit → DI) is fully respected.

---

### Finding 2: Visibility — Both Types Correctly Internal
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs:4`, `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:16`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/IBinanceHttpClient.cs:3-4`, `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:16`

`internal interface IBybitHttpClient` and `internal sealed class BybitHttpClient` match the Binance counterparts exactly. `InternalsVisibleTo` entries in `CryptoExchanges.Net.Bybit.csproj:18-21` expose them to `CryptoExchanges.Net.Bybit.Tests.Integration` and `CryptoExchanges.Net.DependencyInjection`, which are the only two consumers that need direct access. No public surface leaked.

---

### Finding 3: POST Sends JSON Body (Bybit V5 Delta)
- **Severity**: N/A
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:43-45`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:52-53`

`PostAsync<T>` serializes `parameters` (or an empty dictionary) with `JsonSerializer.Serialize` into a `StringContent` with media type `"application/json"`. This is the correct Bybit V5 wire format, explicitly justified in the task manifest (deviation #1). The Binance form-encoding pattern (`application/x-www-form-urlencoded`) does not apply here. The deviation is architecturally sound.

The signing contract is preserved: `BybitSigningHandler.ResignAsync` reads the body back with `request.Content.ReadAsStringAsync` (`BybitSigningHandler.cs:42`) before computing the HMAC. `StringContent` buffers its payload in a `MemoryStream` that is rewindable; `ReadAsStringAsync` re-reads from the beginning on every call, so the body is intact when the handler reads it. Unlike the Binance handler (which disposes and replaces form-encoded content after appending timestamp/signature), the Bybit handler leaves the body untouched and writes auth entirely into headers (`X-BAPI-SIGN`, `X-BAPI-TIMESTAMP`, `X-BAPI-RECV-WINDOW`). This is the correct Bybit V5 signing model and the transport/signing layering separation is clean.

---

### Finding 4: Retry Is GET-Only — POST/DELETE Not Retried
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs:38`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs:33-39`

The shared `ExchangeResiliencePipeline` enforces `if (req?.Method != HttpMethod.Get) return ValueTask.FromResult(false)`. POST and DELETE issued by `BybitHttpClient` flow through this same pipeline. No Bybit-specific pipeline override exists. Architectural invariant #8 is upheld.

---

### Finding 5: Constructor Takes Only HttpClient — No BybitOptions
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:16`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `BybitHttpClient(HttpClient httpClient)` omits `BybitOptions` compared to `BinanceHttpClient(HttpClient httpClient, BinanceOptions options)`. The justification in TASK-005 deviation #3 is that `BybitOptions` is injected solely for `ReceiveWindow` in Binance, and since Bybit carries `recvWindow` in the `X-BAPI-RECV-WINDOW` header (set by `BybitSigningHandler`), the client genuinely has no use for options. `BybitSigningHandler` takes `recvWindow` directly as a constructor parameter (`BybitSigningHandler.cs:14`), so the value flows from options at composition time, not through the HTTP client. The simplification is legitimate and avoids a CS9113 unread parameter under `TreatWarningsAsErrors`.
- **Fix**: No fix needed. If `BybitOptions` later acquires a property that the HTTP client layer needs, inject it at that point.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:16` (Binance injects options because `BuildBaseQuery` reads `ReceiveWindow`; no equivalent exists for Bybit)

---

### Finding 6: GetStringAsync Omitted from Interface
- **Severity**: LOW
- **Confidence**: 45
- **File**: `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs:1-14`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `IBinanceHttpClient` exposes `GetStringAsync` for raw-string responses; `IBybitHttpClient` omits it. The task manifest documents this as deviation #4: not in the TASK-005 required surface, deferrable. At the time of this review, no Bybit service exists that needs raw-string reads. Omitting it keeps the interface minimal (YAGNI). The concern is mild inconsistency between the two exchange interfaces, which could matter if a future consumer abstracts over both via a generic pattern.
- **Fix**: No action required now. If a Bybit service needs raw-string GET, add `GetStringAsync` to `IBybitHttpClient` at that point.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/IBinanceHttpClient.cs:10`

---

### Finding 7: Shared JsonOptions for POST Body Serialization and Response Deserialization
- **Severity**: LOW
- **Confidence**: 40
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:18-23, 43`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: The single static `JsonOptions` instance is used for both `JsonSerializer.Serialize(parameters, JsonOptions)` (POST body) and `ReadFromJsonAsync<T>(JsonOptions, ct)` (response deserialization). The individual options settings are: `PropertyNameCaseInsensitive = true` (deserialization-only, no impact on writing), `NumberHandling.AllowReadingFromString` (deserialization-only), `DefaultIgnoreCondition.WhenWritingNull` (affects serialization — benign for `Dictionary<string,string>` since string values cannot be null in this generic, so no keys are silently dropped). No `PropertyNamingPolicy` is set, so dictionary keys serialize verbatim (correct — callers in services control the key names). The concern would arise if parameters were ever changed to a richer object type with nullable properties that should be included in the wire body.
- **Fix**: No action required now. If `PostAsync` is ever generalized beyond `Dictionary<string,string>`, split into separate read/write options instances.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:18-23` (Binance has the same shared options; acceptable there because form-encoding is used for POST body, not JSON serialization)

---

### Finding 8: Signing is Correctly a Handler Concern, Not a Client Concern
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:31, 46, 57`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:31, 55, 66`

`BybitHttpClient` calls `BybitSigningRequest.MarkSigned(request)` to flag the request and then delegates entirely to the handler chain. No timestamp computation, no HMAC, no `X-BAPI-*` header manipulation exists in the client. All signing logic lives in `BybitSigningHandler` which sits below the retry layer. This perfectly mirrors the Binance pattern and upholds architectural invariant #7.

---

### Finding 9: Build Verification
- **Severity**: N/A
- **Confidence**: 100
- **Verdict**: PASS

`dotnet build CryptoExchanges.Net.sln` reports `Build succeeded. 0 Warning(s) 0 Error(s)` with `TreatWarningsAsErrors=true` in effect.

---

## Summary

- PASS: Layering/dependency direction — no cross-layer `using` or `ProjectReference` violations; Core → Http → Bybit chain respected.
- PASS: Visibility — `IBybitHttpClient` and `BybitHttpClient` are `internal`; `InternalsVisibleTo` entries match Binance pattern exactly.
- PASS: POST JSON body (Bybit V5 delta) — deviation from Binance form-encoding is correctly justified; body is re-readable by `BybitSigningHandler` on retry (StringContent is buffered); signing stays in the handler, not the client.
- PASS: Retry guard — POST/DELETE not retried; shared `ExchangeResiliencePipeline` GET-only guard applies unchanged.
- PASS: Signing as a handler concern — client only marks signed, no inline crypto.
- PASS: Build passes with zero warnings.
- CONCERN: No BybitOptions constructor parameter (confidence: 55, non-blocking) — justified by recv-window moving to signing handler headers; no unread parameter risk.
- CONCERN: GetStringAsync omitted (confidence: 45, non-blocking) — consistent with TASK-005 scope; deferrable.
- CONCERN: Shared JsonOptions for serialize+deserialize (confidence: 40, non-blocking) — benign for `Dictionary<string,string>`; worth a comment for future maintainers.

---

## Final Verdict

APPROVED — confidence 97/100.

All blocking architectural invariants are upheld. The three concerns are non-blocking (all below the 80 confidence threshold) and each has a documented justification in the task manifest. The two reviewed files are a clean, well-scoped implementation that correctly adapts the Binance transport pattern to Bybit V5's JSON-body/header-signing model without introducing any layer violations or hidden coupling.
