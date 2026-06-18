# Security Review — TASK-REF-001
**Branch**: refactor/di-timesync-dry  
**Commits reviewed**: 93ea257 (Phase 1 — TimeSync → Core), 80a5d5a (Phase 2 — shared DI helper)  
**Base SHA**: 3960d4a  
**Reviewer**: Security Reviewer  
**Date**: 2026-06-18  

---

## Overall Verdict: APPROVED

No blocking findings. All four scrutiny gates pass. The refactor is behavior-identical on every security-relevant axis.

---

## Findings

### Finding: Finalizer (signing) gating — Binance, Bybit, OKX
- **Severity**: (audit, not a finding)
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs:58-65`, `src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs:54-64`, `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs:57-67`
- **Verdict**: PASS
- **Issue**: All three `requestFinalizerFactory` lambdas were preserved verbatim inside each exchange's thin delegating call. The gates are identical to the baseline (3960d4a):
  - Binance: `string.IsNullOrEmpty(o.SecretKey)` → `PassThroughHandler`
  - Bybit: `string.IsNullOrEmpty(o.SecretKey)` → `PassThroughHandler`
  - OKX: `string.IsNullOrEmpty(o.SecretKey) || string.IsNullOrEmpty(o.Passphrase)` → `PassThroughHandler`
  
  These lambdas are passed as the `requestFinalizerFactory` delegate and are called verbatim by `ExchangeServiceRegistration` — the helper does not inspect, rewrap, or short-circuit them. The guard is evaluated at resolution time (inside the `http.ApplyResiliencePipeline(…, requestFinalizerFactory)` call), which is the same as the baseline behavior.

---

### Finding: ExchangeTimeSync — no credential material
- **Severity**: (audit, not a finding)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Resilience/ExchangeTimeSync.cs`
- **Verdict**: PASS
- **Issue**: `ExchangeTimeSync` accepts only `long` parameters (`serverTimeMs`, `localNowMs`, `long[] offsetHolder`). No API key, secret key, or passphrase flows through this type at any call site. The three `SyncServerTimeAsync` call sites (`BinanceExchangeClient.cs:105`, `BybitExchangeClient.cs:87`, `OkxExchangeClient.cs:88`) pass `resp.ServerTime`/`serverTimeMs` and `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` — both are plain timestamps.

---

### Finding: ExchangeServiceRegistration — no credential exposure, no transmission of SecretKey
- **Severity**: (audit, not a finding)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs`
- **Verdict**: PASS
- **Issue**: The shared helper contains no references to `ApiKey`, `SecretKey`, or `Passphrase`. It does not read, log, serialize, or transmit any credential. The only credential-touching code (header addition, signing handler construction) remains inside the per-exchange lambdas that are passed in as `configureHttpClient` and `requestFinalizerFactory` — these live in the exchange assemblies, not in the Http shared layer.

---

### Finding: X-MBX-APIKEY header — ApiKey only, not SecretKey
- **Severity**: (audit, not a finding)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs:51-55`
- **Verdict**: PASS
- **Issue**: The `configureHttpClient` lambda adds `X-MBX-APIKEY` with `o.ApiKey` only, conditionally when non-empty. `o.SecretKey` is not referenced anywhere near the default-header path. Bybit and OKX pass `null` for `configureHttpClient`, setting no default authentication header (correct — they authenticate per-request via the signing handler).

---

### Finding: ExchangeServiceRegistration visibility — internal, not public
- **Severity**: (audit, not a finding)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:22`
- **Verdict**: PASS
- **Issue**: The class is declared `internal static class ExchangeServiceRegistration`. It is accessible to the three exchange assemblies only via the pre-existing `InternalsVisibleTo` entries in `CryptoExchanges.Net.Http.csproj` (Binance, Bybit, OKX). No new `InternalsVisibleTo` entries were added. No signing internals are newly exposed on the public SDK surface.

---

### Finding: Rate-limit gate — ReactiveRateLimitGate still hardcoded in helper
- **Severity**: (audit, not a finding)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:126`
- **Verdict**: PASS
- **Issue**: `gateFactory: _ => new ReactiveRateLimitGate()` is hardcoded in the shared helper — identical to the `gateFactory` call that was present in all three baseline `ApplyResiliencePipeline` invocations. The diff shows this was moved from per-exchange to the shared helper; the gate is not lost. All three exchanges are still throttled.

---

### Finding: TryAdd vs Add pattern preserved for keyed singletons
- **Severity**: (audit, not a finding)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:93-129`
- **Verdict**: PASS
- **Issue**: The helper uses `TryAddKeyedSingleton` for `long[]`, `ISymbolMapper`, and `TMapper` (same as baseline), and `AddKeyedSingleton` for `IExchangeClient` (same as baseline). The registration semantics are byte-identical.

---

### Finding: Atomic write via Interlocked.Exchange — preserved
- **Severity**: (audit, not a finding)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Resilience/ExchangeTimeSync.cs:41`
- **Verdict**: PASS
- **Issue**: The previous per-exchange pattern was:
  - Binance/Bybit: `var offset = XxxTimeSync.ComputeOffset(…); Interlocked.Exchange(ref _offsetHolder[0], offset);`
  - OKX: `OkxTimeSync.ApplyOffset(serverTimeMs, localNowMs, _offsetHolder)` (which did the same internally)
  
  All three now call `ExchangeTimeSync.ApplyOffset`, which does `Interlocked.Exchange(ref offsetHolder[0], offset)` at line 41. The thread-safety property is fully preserved. Binance and Bybit now use the atomic `ApplyOffset` path (previously they did the `Interlocked.Exchange` inline — functionally equivalent).

---

## Summary

- PASS: Finalizer/signing gating — all three exchanges preserve their credential-present/absent gate exactly; the shared helper delegates to per-exchange lambdas without inspection (confidence: 98/100)
- PASS: ExchangeTimeSync — handles only `long` epoch timestamps; zero credential material flows through it (confidence: 99/100)
- PASS: ExchangeServiceRegistration — no `ApiKey`/`SecretKey`/`Passphrase` references; all credential-touching code remains in per-exchange lambdas (confidence: 99/100)
- PASS: X-MBX-APIKEY header adds only `ApiKey`, not `SecretKey`; Bybit/OKX set no default auth header (confidence: 99/100)
- PASS: `ExchangeServiceRegistration` is `internal`; no new public-surface exposure of signing internals (confidence: 99/100)
- PASS: `ReactiveRateLimitGate` still registered for all three exchanges; rate limiting not regressed (confidence: 99/100)
- PASS: `TryAdd`/`Add` keyed-singleton pattern preserved byte-for-byte (confidence: 99/100)
- PASS: `Interlocked.Exchange` atomicity of the clock-skew offset write preserved (confidence: 99/100)

**No blocking findings. No CONCERNs. APPROVED.**
