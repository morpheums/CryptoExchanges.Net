# API Review — TASK-005: IBybitHttpClient + BybitHttpClient

**Reviewer**: API Reviewer
**Commit**: 2a598c8
**Date**: 2026-06-17
**Files reviewed**:
- `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs`
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs`

---

### Finding 1: Interface contract is consistent with Binance pattern and ergonomically correct
- **Severity**: LOW
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs:7-13`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. The three-method surface (`GetAsync<T>`, `PostAsync<T>`, `DeleteAsync<T>`) exactly mirrors `IBinanceHttpClient` minus `GetStringAsync`. Parameter order, names, and types are identical across all three methods. Default-argument choices (`signed = false` for GET, `signed = true` for POST/DELETE) match the Binance pattern and are semantically correct for Bybit V5: public market data endpoints are unsigned GETs; all state-mutating and account endpoints require authentication.
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/IBinanceHttpClient.cs:7-16`

---

### Finding 2: GetStringAsync omission — not a gap for this task, but documented risk for GetCandlesticksAsync
- **Severity**: MEDIUM
- **Confidence**: 82
- **File**: `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs:4-14` vs `src/CryptoExchanges.Net.Binance/IBinanceHttpClient.cs:10`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 82/100)
- **Issue**: `IBinanceHttpClient.GetStringAsync` exists solely because `BinanceMarketDataService.GetCandlesticksAsync` (`BinanceMarketDataService.cs:233`) uses it — Binance's `/api/v3/klines` returns an array-of-arrays that does not map to a typed DTO, so the implementation calls `GetStringAsync` and parses with `JsonDocument` manually. Bybit's `/v5/market/kline` returns a standard `{"result":{"list":[...]}}` envelope that is typed-DTO-friendly. If TASK-006 follows that approach, `GetStringAsync` is genuinely unnecessary and the omission is correct. However, Bybit klines each candle is itself an array `["timestamp","open","high","low","close","volume","turnover"]` inside the list, which may still require raw-string parsing. If so, `GetStringAsync` will need to be added to the interface at that point — a deferred change to a type already consumed by TASK-006 and TASK-008.
- **Fix**: No immediate action required. When implementing `BybitMarketDataService.GetCandlesticksAsync` in TASK-006, verify whether `GetAsync<BybitKlineEnvelope>` works cleanly. If the inner list is an array-of-arrays, add `GetStringAsync` to `IBybitHttpClient` before or alongside TASK-006 rather than mid-task.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Services/BinanceMarketDataService.cs:233`

---

### Finding 3: POST body stream safe for re-reads by signing handler
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:43-44` + `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:42`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. The client creates a `StringContent` wrapping a `string`. `StringContent` is backed by a `MemoryStream` that is seeked to 0 automatically on each `ReadAsStringAsync` call — it is safe to read multiple times. The signing handler reads the content at line 42 and does NOT replace it (unlike `BinanceSigningHandler` which re-creates a new `StringContent` after reading). Since `StringContent` is inherently re-readable, reading it in the handler then passing the same instance through to the inner handler is safe and the wire body will be identical to what was signed.
- **Fix**: N/A

---

### Finding 4: Null-parameter POST edge case emits `{}` — correct behavior
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:43`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. When `parameters` is null or empty, `PostAsync<T>` serializes `parameters ?? []` as `{}`. This is correct for Bybit V5: signed POST requests with no parameters must still send `{}` as the JSON body, because `BybitSigningHandler.BuildPostSignString` signs the body verbatim — the sign-string must be `timestamp+apiKey+recvWindow+{}` not an empty suffix. Sending an empty body would cause a signature mismatch. The empty-dict-to-`{}` behavior is intentional and correct.
- **Fix**: N/A

---

### Finding 5: JsonOptions static field matches Binance exactly
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:18-23` vs `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:18-23`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `PropertyNameCaseInsensitive`, `AllowReadingFromString`, and `WhenWritingNull` are identical across both clients. Cross-exchange DTO deserialization behavior is consistent.
- **Fix**: N/A

---

### Finding 6: No endpoint null guard
- **Severity**: LOW
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:26-60`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 75/100)
- **Issue**: None of the three `*Async` methods guard against `endpoint` being null or empty. `BuildUrl` would produce a URI like `?key=value` if endpoint were empty, and `HttpRequestMessage` would throw an opaque exception on construction. Binance has the same gap, so this is not a regression, but it is an ergonomic concern for service authors.
- **Fix**: Optionally add `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` at the top of each method. Not required to unblock TASK-006.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:39`

---

### Finding 7: XML doc adequacy
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs:3-13`, `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:7-15`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. The interface carries a summary on every member, and the class carries an extended class-level summary documenting architectural intent (JSON body vs form-encoded, recv-window header placement, resilience pipeline contract). The implementation uses `<inheritdoc />` on all three methods. Adequate for an internal shared contract.
- **Fix**: N/A

---

### Finding 8: InternalsVisibleTo coverage is correct
- **Severity**: LOW
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:17-22`
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: None. `InternalsVisibleTo` is granted only to `CryptoExchanges.Net.Bybit.Tests.Integration` and `CryptoExchanges.Net.DependencyInjection`. No consumer application project is granted visibility. Consistent with the Binance csproj pattern.
- **Fix**: N/A

---

### Finding 9: BybitSigningRequest and BybitSignatureService are public — out of TASK-005 scope
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:6`, `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 70/100)
- **Issue**: Both types are `public sealed class` rather than `internal sealed class`, exporting signing primitives on the NuGet surface. Same pattern as Binance, so not a regression, but worth tracking ahead of the OKX/Bitget generalization where signing primitives may be shared across assemblies and access level choices become architecturally significant. Outside TASK-005 scope.
- **Fix**: Track as a candidate for access-level review during the OKX signing generalization task.

---

### Summary

- PASS: Interface method signatures — identical shape to `IBinanceHttpClient`, correct `signed` defaults, correct `CancellationToken ct = default` last position, `Dictionary<string,string>?` nullable parameters.
- PASS: JSON body for POST — `StringContent` with `application/json` correct for Bybit V5; body safely re-readable by `BybitSigningHandler`.
- PASS: `{}` emitted for null/empty POST parameters — correct for Bybit V5 signing contract.
- PASS: JsonOptions — exact match with Binance; consistent cross-exchange deserialization.
- PASS: InternalsVisibleTo scope — limited to test and DI projects only; no consumer exposure.
- PASS: XML documentation — adequate for an internal shared contract.
- CONCERN: `GetStringAsync` omission — justified today but likely to resurface in TASK-006 when implementing `GetCandlesticksAsync` (confidence: 82/100, non-blocking). Verify DTO shape before declaring omission permanent.
- CONCERN: No `endpoint` null guard — same gap as Binance; low risk for internal callers (confidence: 75/100, non-blocking).
- CONCERN: `BybitSigningRequest`/`BybitSignatureService` are `public` — out of TASK-005 scope; track for OKX generalization (confidence: 70/100, non-blocking).

---

## Final Verdict

APPROVED — Confidence: 93/100

The interface contract is well-formed, internally consistent, and correctly mirrors the Binance pattern with documented and justified deviations (JSON body over form-encoding, no `recvWindow` in query, no `GetStringAsync` for this scope). The `signed` defaults are ergonomically correct. The body signing pipeline is technically sound — `StringContent` is re-readable without stream position management. No blocking issues found. The `GetStringAsync` concern should be tracked by the TASK-006 implementer before merging that task.
