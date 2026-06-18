# Architect Review — TASK-003: BybitSigningHandler

**Reviewer**: Architect Reviewer
**Commit**: 283bcf0
**Branch**: feat/m2-exchange-expansion
**Date**: 2026-06-17

## Files Reviewed

- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs` (new)
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs` (doc cref touch only)

---

## Findings

### Finding: Layering integrity
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:1-4`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:11-14`

`BybitSigningHandler.cs` lives in `CryptoExchanges.Net.Bybit.Resilience`, carries only two `using` directives (`System.Globalization` and `CryptoExchanges.Net.Bybit.Auth`), and the `.csproj` is unchanged. No new `ProjectReference` nodes, no touch to Core or Http layers. The handler slots in as `requestFinalizer` in `HttpClientPipelineBuilder.Build()` without requiring any Http-layer change.

---

### Finding: Pattern fidelity with BinanceSigningHandler
- **Severity**: N/A
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13-62`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:12-30`

The structural mirror is faithful: `ArgumentNullException.ThrowIfNull(request)`, Remove-then-Add API-key header gated on `!string.IsNullOrEmpty(apiKey)`, `IsSigned(request)` gate before `ResignAsync`, `ConfigureAwait(false)` on every `await`, and identical timestamp formula with `CultureInfo.InvariantCulture`.

---

### Finding: Deviation 1 — signature in headers, no StringContent swap
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:40-60`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:37-46`

Bybit places the signature in `X-BAPI-SIGN`, not the query string or body. The handler reads the body read-only via `ReadAsStringAsync` (no mutation, no Dispose/replace cycle). The strip-and-re-add pattern for `X-BAPI-TIMESTAMP`, `X-BAPI-RECV-WINDOW`, and `X-BAPI-SIGN` before adding fresh values satisfies acceptance criterion 2 (single, not doubled, headers on retry).

---

### Finding: Deviation 2 — recvWindow string constructor parameter
- **Severity**: N/A
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:14`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:12-13`

`BybitOptions.ReceiveWindow` is `decimal`. Accepting it as a pre-formatted `string` keeps the handler agnostic of `BybitOptions`, mirrors how Binance takes `apiKey` as plain `string`, and delegates formatting to the composer. Both `BuildGetSignString` and `BuildPostSignString` enforce `ThrowIfNullOrWhiteSpace` — no silent empty-string signing risk.

---

### Finding: DelegatingHandler placement below retry
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/HttpClientPipelineBuilder.cs:37-44`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Http/HttpClientPipelineBuilder.cs:37-44`

`HttpClientPipelineBuilder.Build()` places `requestFinalizer` between `ResilienceHandler` and `ErrorTranslationHandler`. The signing handler receives `SendAsync` on each individual attempt, ensuring a fresh timestamp is computed after every retry delay.

---

### Finding: GET-only retry invariant untouched
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs:33-44`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs:33-44`

No change to `ExchangeResiliencePipeline.Configure()`. The GET-only retry guard is untouched.

---

### Finding: Access modifier discipline
- **Severity**: N/A
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:12`

`BybitSigningHandler` is `public sealed` — consistent with `BinanceSigningHandler`. No previously-internal type was made public in this diff.

---

### Finding: DELETE method falls through to GET signing path without explicit branch
- **Severity**: LOW
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:40`
- **Category**: Architecture
- **Verdict**: CONCERN
- **Issue**: The branch condition is `request.Method == HttpMethod.Post && request.Content is not null`. DELETE (and any future method) falls through implicitly to the GET/query-string path. This is correct per Bybit spec today, but is not self-documenting about the intended GET/DELETE grouping. A future PUT addition would silently fall to the wrong path.
- **Fix**: Add an inline comment on the else branch: `// GET, DELETE, and all other methods: sign over query string` to make intent explicit.
- **Pattern reference**: `nazgul/tasks/TASK-003.md:54` (task description explicitly states GET/DELETE signs over query string)

---

### Finding: No global or static mutable state
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13-14`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: N/A

All fields are captured constructor parameters. None are static, none are mutable.

---

### Finding: Build verification
- **Severity**: N/A
- **Confidence**: 100
- **File**: `CryptoExchanges.Net.sln`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: N/A

`dotnet build CryptoExchanges.Net.sln` completed with 0 Warning(s), 0 Error(s).

---

## Summary

- PASS: Layering integrity — no Core/Http modifications, no dependency direction violations. (confidence: 99)
- PASS: BinanceSigningHandler pattern mirrored correctly — structure, guards, ConfigureAwait, timestamp formula, API-key header handling. (confidence: 98)
- PASS: Deviation 1 (header-based signature) — omitting StringContent swap is correct; strip-then-add header pattern satisfies the re-sign-on-retry requirement. (confidence: 97)
- PASS: Deviation 2 (`recvWindow` as pre-formatted string) — justified; keeps handler options-agnostic, formatting responsibility delegated to composer. (confidence: 95)
- PASS: DelegatingHandler placement below Polly retry — fresh timestamp per attempt, recvWindow rejection risk eliminated. (confidence: 99)
- PASS: GET-only retry invariant untouched. (confidence: 99)
- PASS: No previously-internal types made public; access modifiers consistent with Binance. (confidence: 98)
- PASS: No static mutable state; thread-safe by construction. (confidence: 100)
- PASS: Build 0 warnings, 0 errors. (confidence: 100)
- CONCERN: DELETE/other methods fall through to GET path implicitly — correct today but branch condition should carry an explicit comment to guard against future method additions. (confidence: 72, non-blocking)

---

## Final Verdict

APPROVED

The implementation is architecturally sound. All invariants from the review checklist pass. The single CONCERN (implicit DELETE fallthrough) is non-blocking at confidence 72 — it is a minor documentation/defensive-coding observation, not a correctness flaw. No changes are required before this task proceeds.
