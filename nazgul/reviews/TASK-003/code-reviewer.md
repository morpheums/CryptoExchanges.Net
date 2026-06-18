# Code Review: TASK-003 — BybitSigningHandler

**Reviewer**: Code Reviewer (claude-sonnet-4-6)
**Date**: 2026-06-17
**Commit**: 283bcf0
**Files reviewed**:
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs` (new)
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs` (doc-cref touch)

**Supporting types consulted**:
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs`
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs` (pattern reference)

**Build**: `dotnet build CryptoExchanges.Net.sln` — 0 Warning(s), 0 Error(s)
**Tests**: 90/90 unit tests pass (Bybit-specific tests deferred to TASK-008 per manifest)

---

## Findings

### Finding 1: POST with null Content silently falls to GET/query-signing branch
- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:40-49`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking, confidence 65)
- **Issue**: The condition `request.Method == HttpMethod.Post && request.Content is not null` means a `POST` with no body silently signs over the query string instead of an empty body string. Bybit's sign-string formula for POST is `timestamp+apiKey+recvWindow+jsonBody`; using the query string instead is semantically wrong. In practice `BybitHttpClient.PostAsync` (line 44-45) always attaches a `StringContent`, so this path is not reachable today. However, any future caller who issues a signed POST without a body will get a silently miscalculated signature that Bybit will reject — no exception, no log, just a 401.
- **Fix**: Treat `POST` with null content as POST with an empty body: change the branch to `if (request.Method == HttpMethod.Post)` and pass `request.Content is not null ? await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false) : string.Empty` to `BuildPostSignString`.
- **Pattern reference**: Condition already reads null-safe (`request.Content is not null`) — fix is keeping the POST formula when method is POST regardless of content nullability.

---

### Finding 2: Missing null/empty guards on primary constructor parameters (signatureService, recvWindow, timeOffset)
- **Severity**: MEDIUM
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13-14`
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking, confidence 55)
- **Issue**: The project rule states every public/internal method must have guards. `signatureService` (reference type) gets no `ArgumentNullException.ThrowIfNull`; `recvWindow` (a new parameter not present in `BinanceSigningHandler`) gets no `ArgumentException.ThrowIfNullOrWhiteSpace`; `timeOffset` (`Func<long>`) gets no null check. If `signatureService` is null, the crash surfaces inside `ResignAsync` at `signatureService.Sign(...)` as a `NullReferenceException` rather than a named `ArgumentNullException`. Confidence is 55 rather than high because `BinanceSigningHandler` follows the same no-constructor-guard pattern, establishing a local precedent.
- **Fix**: Add field-initializer guards using the private-static factory pattern that `BybitSignatureService` itself uses for `secretKey` (`BybitSignatureService.cs:63-66`): assign `_signatureService = Guard(signatureService)` and `_recvWindow = GuardNonEmpty(recvWindow)` via private static helpers, or inline `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrWhiteSpace` in the field initializers.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:63-66` (private static `InitializeSecretKey` guards before returning the backing field value)

---

### Finding 3: `RequestUri` null handled with silent empty-string fallback rather than fast-fail
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:47`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking, confidence 70)
- **Issue**: `request.RequestUri?.Query.TrimStart('?') ?? string.Empty` — when `RequestUri` is null the handler signs over an empty query string and proceeds, placing headers that Bybit will reject with no exception and no log. This differs from `BinanceSigningHandler.cs:49` which uses `request.RequestUri!` (null-forgiving), meaning a null URI throws `NullReferenceException` immediately. The `!` dereference in Binance is an intentional fast-fail; the `?.` here is a silent swallow.
- **Fix**: Use `request.RequestUri!.Query.TrimStart('?')` (matching the Binance pattern) or add an explicit `ArgumentNullException.ThrowIfNull(request.RequestUri)` before query extraction.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:49` (`var uri = request.RequestUri!;`)

---

### Finding 4: Acceptance criteria #2 — re-sign on retry produces single header set
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:55-60`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. Strip-then-add pattern on `X-BAPI-TIMESTAMP`, `X-BAPI-RECV-WINDOW`, `X-BAPI-SIGN` is correct. `HttpRequestHeaders.Remove` is a no-op when the header is absent (first attempt) and removes all values when present (subsequent attempts), so two consecutive `SendAsync` calls produce exactly one set of headers with a fresh timestamp.

---

### Finding 5: Acceptance criteria #3 — unsigned passthrough except api-key
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:22-31`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. The `if (BybitSigningRequest.IsSigned(request))` gate strictly controls entry into `ResignAsync`. Unsigned requests receive only the API-key header (only when `apiKey` is non-empty), with no timestamp, recv-window, or signature headers added.

---

### Finding 6: ConfigureAwait(false) and CancellationToken forwarding
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:29, 31, 42`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. Every `await` has `.ConfigureAwait(false)`. `CancellationToken` is forwarded to `ReadAsStringAsync` (line 42) and `base.SendAsync` (line 31). No `OperationCanceledException` catch blocks exist; cancellation propagates correctly.

---

### Finding 7: POST body re-read on retry
- **Severity**: N/A
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:42`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `StringContent` wraps a `MemoryStream` whose read position is reset by `HttpContent.LoadIntoBufferAsync` before each `ReadAsStringAsync` call. Re-reading the body on retry is safe. The decision not to replace the content object (unlike Binance) is correctly justified in the manifest — the signature lives in headers, so the body is read-only.

---

### Finding 8: CultureInfo.InvariantCulture on timestamp
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:36-37`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. Matches `BinanceSigningHandler.cs:34-35` exactly.

---

### Finding 9: Primary-constructor style, sealed, DelegatingHandler inheritance
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13-15`
- **Category**: Style
- **Verdict**: PASS
- **Issue**: None. Primary constructor used (C# 12 style per project conventions). `sealed` applied. Inherits `DelegatingHandler` correctly. Naming follows `PascalCase` convention throughout.

---

### Finding 10: XML documentation
- **Severity**: N/A
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:6-11`
- **Category**: Code Quality
- **Verdict**: PASS
- **Issue**: None. Class-level `/// <summary>` is present and substantive. `/// <inheritdoc />` is used on the overridden `SendAsync` (correct pattern matching `BinanceSigningHandler.cs:17`). `ResignAsync` is `private` so no doc required. CS1591 is suppressed in the `.csproj` (line 8), so missing `<param>` tags on the primary constructor are not a build issue and are consistent with `BinanceSigningHandler`.

---

## Summary

- PASS: Acceptance criteria #1 (sign-string construction) — GET signs over query, POST signs over body, `CultureInfo.InvariantCulture` on timestamp, correct static helper dispatch.
- PASS: Acceptance criteria #2 (re-sign on retry) — strip-then-add produces a single fresh header set on every attempt.
- PASS: Acceptance criteria #3 (unsigned passthrough) — `IsSigned` gate correctly placed; no signing headers leak to unsigned requests.
- PASS: `ConfigureAwait(false)` on every await, CT forwarded to both `ReadAsStringAsync` and `base.SendAsync`.
- PASS: POST body re-read on retry is safe (StringContent/MemoryStream); no content swap needed or performed.
- PASS: `sealed`, primary constructor, `/// <inheritdoc />` pattern — all match Binance reference.
- CONCERN: POST with null Content falls to GET/query branch — signs over query string instead of empty body string, silently producing a wrong signature for any future signed POST without a body. (confidence: 65/100, non-blocking)
- CONCERN: No null/empty guards on `signatureService`, `recvWindow`, `timeOffset` constructor parameters — diverges from project guard mandate, though consistent with `BinanceSigningHandler`'s own gap. (confidence: 55/100, non-blocking)
- CONCERN: `RequestUri?.Query ... ?? string.Empty` silently signs over empty string when URI is null, unlike Binance's fast-fail `!` dereference. (confidence: 70/100, non-blocking)

---

## Final Verdict

**APPROVED**

No blocking issues. All three acceptance criteria are correctly implemented. The build is clean at 0 warnings. The three concerns are non-blocking: the POST/null-Content path is unreachable given `BybitHttpClient.PostAsync` always attaches a body; the constructor guard gap mirrors the existing Binance precedent; the `RequestUri` null path is equally unreachable in normal pipeline operation. They are worth addressing in a follow-up polish pass or when TASK-008 unit tests are written (the test scaffolding will surface them naturally).
