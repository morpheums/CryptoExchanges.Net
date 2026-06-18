# Security Review — TASK-009B: Per-exchange DI re-homing (ADR-001)

**Reviewer**: Security Reviewer
**Commit**: 1a56835
**Branch**: feat/m3-okx
**Date**: 2026-06-18

---

## Mandate Coverage

### 1. Binance signing types: public -> internal

**Finding: Binance signing types correctly internalized**
- **Severity**: N/A (positive finding)
- **Confidence**: 100
- **Files**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:9`, `Resilience/BinanceSigningHandler.cs:12`, `Resilience/BinanceSigningRequest.cs:5`
- **Verdict**: PASS
- **Issue**: None. `BinanceSignatureService`, `BinanceSigningHandler`, and `BinanceSigningRequest` are all changed from `public` to `internal sealed` / `internal static`. Attack surface is reduced: external consumers can no longer directly instantiate or reference these types.
- **HMAC integrity**: `BinanceSignatureService` stores the secret as `byte[] _secretKeyBytes` — the raw string is not persisted in a field after `InitializeSecretKey` runs. The secret is never transmitted; only the HMAC output appears in the query string as `signature=...`. Signing pipeline is intact.
- **IVT scope**: `InternalsVisibleTo("CryptoExchanges.Net.Binance.Tests.Integration")` is the only IVT on the Binance csproj. Tests (`BinancePipelineEndToEndTests`, `BinanceSigningHandlerTests`) reference these internal types directly and this IVT covers them. The previously-present `InternalsVisibleTo("CryptoExchanges.Net.DependencyInjection")` was correctly removed — the DI package no longer touches Binance internals. No external production assembly has IVT to Binance internals.

**Note on `MarkSigned`/`IsSigned` accessibility**: These methods are `public static` on the `internal` class `BinanceSigningRequest`. In C# the `public` modifier on members of an `internal` type has no effect outside the assembly — the effective accessibility is `internal`. This is correct and conventional; the `public` member keyword is kept so callers within the assembly (primarily `BinanceHttpClient`) can call them without any accessibility gymnastics. No re-exposure risk.

---

### 2. Secret-gated finalizer: behavior preservation after move

**Finding: Secret-gated finalizer logic is behavior-preserving**
- **Severity**: N/A (positive finding)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs:94-102`
- **Verdict**: PASS
- **Issue**: None. The moved logic is verbatim-identical to the original DI package body.
  - `SecretKey` empty → `return new PassThroughHandler()` (no signing, no HMAC, no API key injected by handler). The API key is still added as a default header on the `HttpClient` (line 75-76), which is correct and pre-existing behavior.
  - `SecretKey` present → `BinanceSigningHandler` is constructed with `o.ApiKey`, `new BinanceSignatureService(o.SecretKey)`, and the clock-skew closure. Signing fires on every attempt.
- There is no code path where a secret is present but signing is silently skipped. There is no path where signing is applied when no secret is present.
- The `requestFinalizerFactory` lambda runs at DI resolution time (singleton scope), so the gate reads the fully-configured `BinanceOptions` (env defaults + explicit configure action applied in correct order at lines 43-48).

---

### 3. BinanceHttpClient endpoint guards

**Finding: Endpoint guards are correctly placed and do not affect signing**
- **Severity**: N/A (positive finding)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:30, 42, 54, 68`
- **Verdict**: PASS
- **Issue**: None. `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` is added as the first statement in all four methods (`GetAsync<T>`, `GetStringAsync`, `PostAsync<T>`, `DeleteAsync<T>`). Guards throw before any URL is constructed, before any `MarkSigned` call, before any HTTP send. This cannot cause double-signing, parameter duplication, or request construction side effects. It matches the Bybit pattern from PR #11.
- `Uri.EscapeDataString` escaping of query parameters is still present at `BinanceHttpClient.cs:96` and is unaffected by this change.

---

### 4. Secret exposure audit: no new logging/serialization paths

**Finding: No new secret exposure introduced**
- **Severity**: N/A (positive finding)
- **Confidence**: 100
- **Files reviewed**: All files touched in the diff
- **Verdict**: PASS
- **Details**:
  - `BinanceSignatureService`: secret stored as `byte[]`, not as a string field. No `ToString()` override. No serialization attribute.
  - `BinanceSigningHandler`: `apiKey` stored as constructor parameter reference (string). Not logged, not serialized, not included in any exception message. No `ToString()` override.
  - `BinanceOptions` / `BybitOptions`: no `[JsonInclude]` annotation. No `ToString()` override. Plain POCO — pre-existing and unchanged by this commit.
  - `CryptoExchangesOptions` (DI package): `BinanceSecretKey` and `BybitSecretKey` are plain `string?` properties. No `[JsonInclude]`, no `ToString()` override, no serialization path introduced. These properties are passed into the per-exchange `configure` delegate and then discarded; they are not stored in any singleton.
  - `ExchangeClientFactory` (relocated to Http): resolves `IExchangeClient` via keyed DI only. No credential awareness, no logging, no serialization.

---

### 5. Env-default reads: unchanged

**Finding: ApplyEnvDefaults is verbatim-identical to prior implementation**
- **Severity**: N/A (positive finding)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs:116-122`
- **Verdict**: PASS
- **Issue**: None. `BINANCE_API_KEY` → `o.ApiKey`; `BINANCE_SECRET_KEY` → `o.SecretKey`. Guard is `!string.IsNullOrEmpty` (correct — empty env vars are treated as unset). The configure chain order (`ApplyEnvDefaults` first, then the caller's `configure` action) means explicit caller configuration always wins over env vars, which is the intended and pre-existing semantics.

---

### Additional checks (from review mandate)

**Signing pipeline position**: `BinanceSigningHandler` is registered via `requestFinalizerFactory` inside `ApplyResiliencePipeline`. The handler sits INSIDE the Polly retry boundary (outer→inner: throttle → exhaustion-mapping → Polly → **signing** → error-translation → transport). The mark-and-strip pattern in `BinanceSigningRequest`/`BinanceSigningHandler.StripSigning` prevents duplicate `timestamp=`/`signature=` params on retry. This is unchanged by the move.

**Rate limiting**: `ReactiveRateLimitGate` is still registered via `gateFactory: _ => new ReactiveRateLimitGate()` — no regression.

**Error translator**: `BinanceErrorTranslator` is still registered via `translatorFactory`. Rate-limit and auth error classifications are unchanged.

**ExchangeClientFactory IVT**: `CryptoExchanges.Net.Http.csproj` grants `InternalsVisibleTo` to `CryptoExchanges.Net.Binance` and `CryptoExchanges.Net.Bybit` only — exactly the two assemblies that call `TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>()`. No test assembly, no DI package, and no other production assembly has this grant. The factory itself (`internal sealed`) never touches credentials.

---

## Summary

- PASS: Binance signing types internalized — attack surface reduced, HMAC integrity preserved, IVT scope correctly narrowed to test assembly only
- PASS: Secret-gated finalizer — verbatim move, behavior-preserving; PassThrough on empty secret, BinanceSigningHandler only when secret present
- PASS: BinanceHttpClient endpoint guards — correct boundary, no signing interaction, matches Bybit pattern
- PASS: No secret exposure — no new logging, serialization, or inappropriate field capture of ApiKey/SecretKey in any modified file
- PASS: Env-default reads — unchanged, correct guard, correct configure-chain order
- PASS: ExchangeClientFactory relocation — internal, no credential awareness, IVT scope minimal and correct
- PASS: Signing pipeline position — mark-and-strip pattern intact, retry deduplication preserved
- PASS: Rate limiting and error translation — no regression

---

## Final Verdict

**APPROVED**

Confidence: 98/100

No blocking findings. All security-relevant invariants are preserved:
- The SecretKey is never transmitted, never stored as a plain string field beyond the IOptions resolution lambda, and never logged.
- Signing is applied if and only if a secret is present.
- The internalization of signing types reduces the public API surface without creating any regression.
- IVT grants are minimal and correctly scoped.

The 2-point confidence deduction is for the pre-existing `CryptoExchangesOptions.BinanceSecretKey` / `BybitSecretKey` public properties which lack a `ToString()` redaction override — this is a pre-existing condition not introduced by this commit, and is noted as a non-blocking observation for a future hardening pass.
