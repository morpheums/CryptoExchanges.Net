# Architect Review — TASK-014: OkxHttpClient + IOkxHttpClient

**Reviewer**: Architect Reviewer (claude-sonnet-4-6)
**Date**: 2026-06-18
**Branch**: feat/m3-okx
**Files under review**:
- `src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs`
- `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs`

**Context files read (cross-consistency only, not in scope)**:
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`
- `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs`
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs` (reference pattern)
- `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj`

---

## Sign-Consistency Invariant Analysis

The central scrutiny question: does `OkxHttpClient` build request URIs such that
`request.RequestUri.PathAndQuery` (read by `OkxSigningHandler.ResignAsync` at line 52) is
byte-for-byte equal to the `requestPath` that OKX signs?

### GET / DELETE (BuildUrl path)

`BuildUrl` concatenates the caller-supplied endpoint string with a `?`-prefixed query string built
by `BuildQueryString`. The result is passed directly as the `requestUri` string argument to
`new HttpRequestMessage(HttpMethod.Get, ...)`.

When `HttpClient.BaseAddress` is a host-only URI such as `https://www.okx.com` (no trailing path
segment), the .NET runtime resolves the relative string against that base using standard URI
combining. For a relative string beginning with `/` the combined result is
`https://www.okx.com/api/v5/...?key=val`, and `RequestUri.PathAndQuery` becomes
`/api/v5/...?key=val` — exactly what `BuildUrl` produced. No double-encoding or re-ordering occurs
because `PathAndQuery` is read directly from the same URI object .NET built; it does not go through
a second encode/decode cycle.

**Query param ordering**: The same `Dictionary<string, string>` instance is passed into
`BuildQueryString` once, which iterates it once, appending pairs in enumeration order. The handler
reads `PathAndQuery` back from the fully-resolved `RequestUri` — it does not re-iterate the
dictionary. There is no mechanism for order to desync between the string that was built and the
string that gets signed.

**Uri.EscapeDataString vs HttpClient wire encoding**: The query string is built via
`Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value)` and baked into the relative URI
string. The .NET `Uri` constructor for an already-percent-encoded string will NOT double-encode it
(it recognizes valid percent sequences). `RequestUri.PathAndQuery` reads the path and query as
stored in the `Uri` object — the same escaped bytes the client wrote — so the signed string and the
wire bytes are identical.

**Trailing "?" edge**: `BuildUrl` only appends the separator when `query` is non-empty
(`string.IsNullOrEmpty(query)`). If there are no parameters the endpoint string is returned bare,
so `RequestUri.PathAndQuery` has no trailing `?`. No stray `?` is introduced.

**BaseAddress containing a path segment**: If someone mistakenly configures
`BaseAddress = "https://www.okx.com/api/"` (with a path), .NET's URI combining rules for a
base with a path + a relative that starts with `/` would discard the base path and use only the
host, so `/api/v5/market/tickers` would still resolve correctly. However, if the relative string
did NOT start with `/` (see Finding 1 below), the resolution would be against the base path, which
could produce an incorrect `PathAndQuery`. This concern is partially mitigated by documentation in
the class XML doc and is a configuration concern owned by the DI/composer layer (TASK-013), not by
this file. See Finding 1 for the low-confidence observation.

### POST path

`PostAsync` passes `endpoint` directly (no `BuildUrl`) as the URI, and parameters go into the JSON
body via `JsonSerializer.Serialize(parameters ?? [], JsonOptions)`. The handler reads the body back
via `request.Content.ReadAsStringAsync(ct)` at line 59 of `OkxSigningHandler.cs`. Since the
handler reads the same `StringContent` that the client set — with no intermediate re-serialization
— the signed body string is byte-for-byte the wire body. This path is clean.

### Separation-of-concerns / signing leakage

`OkxHttpClient` never performs any HMAC computation. It delegates the sign decision by calling
`OkxSigningRequest.MarkSigned(request)`, which sets an `HttpRequestOptions` flag. All cryptographic
work is in `OkxSigningHandler`. Concern does not exist.

### Constructor / layering (ADR-001)

Primary constructor `OkxHttpClient(HttpClient httpClient)` takes only an `HttpClient`. No
credentials, no options, no service locator. Fully consistent with ADR-001.

### Bybit pattern conformance

The implementation is a near-exact structural copy of `BybitHttpClient` (same ctor shape, same
`BuildUrl`/`BuildQueryString` helpers, same `JsonOptions` statics, same `MarkSigned` delegation,
same `ReadFromJsonAsync` deserialization). The only OKX-specific differences are namespace and the
`OkxSigningRequest` reference — both correct.

---

## Findings

### Finding 1: Endpoint without leading slash could silently mis-sign if BaseAddress has a path

- **Severity**: LOW
- **Confidence**: 35
- **File**: `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs:44,73,87`
- **Category**: Architecture (sign-consistency edge case)
- **Verdict**: PASS
- **Issue**: If a caller passes an endpoint WITHOUT a leading `/` (e.g. `"api/v5/market/tickers"`
  instead of `"/api/v5/market/tickers"`) AND `BaseAddress` contains a path (e.g.
  `https://www.okx.com/api/`), .NET URI combining would append to the base path, producing a
  `PathAndQuery` of `/api/api/v5/market/tickers` — which would be signed but would not match OKX's
  expected path. However:
  (a) the class XML doc explicitly states "Callers pass the full path (e.g. `/api/v5/market/tickers`)
      as the endpoint" with a leading slash,
  (b) the expected `BaseAddress` is documented as host-only (`https://www.okx.com`), and
  (c) enforcement of both invariants is a responsibility of the composer/DI layer (TASK-013), not
      this file.
  Confidence is intentionally low (35) because the scenario requires two independent misconfiguration
  errors simultaneously. This is a documentation concern, not a code bug in TASK-014.
- **Fix**: No code change required in TASK-014. The DI/composer task (TASK-013) should guard that
  `BaseAddress` is host-only and consider adding an `ArgumentException` or a startup validation that
  rejects endpoints not starting with `/`.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs` — identical situation;
  same documentation-only mitigation is accepted there.

### Finding 2: POST parameters typed as Dictionary<string, string> — structural limitation

- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs:16`, `OkxHttpClient.cs:58`
- **Category**: Architecture (OCP / future-proofing)
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `parameters` for POST is `Dictionary<string, string>?`, which serializes to
  `{"key":"value"}`. This is fine for simple OKX endpoints, but many OKX V5 POST endpoints accept
  nested structures (e.g. `algoOrd`, `attachAlgoOrds`, `tpOrdPx` arrays). Callers who need nested
  structures would have to work around the flat dict by calling a different overload or using a raw
  JSON approach. This is the same limitation accepted in the Bybit implementation, so it is a
  pattern-level issue, not a TASK-014 regression. As noted in the milestone review guidance, cloning
  the pattern an Nth time materializes a latent smell across 3+ copies.
- **Fix (non-blocking)**: Consider adding a future `PostAsync<TBody, TResponse>` overload accepting
  `object body` / a typed DTO directly, or changing the parameter type to `object?` before the
  interface is locked. This is a design suggestion for the team to consider at the next milestone
  boundary — not a TASK-014 blocker.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:45` — identical
  `Dictionary<string, string>` serialization; same limitation already accepted.

---

## Checklist Results

- [x] No `using` or `ProjectReference` in Core pointing to Http, Binance, OKX, or DI — N/A (OKX
      project is not Core or Http).
- [x] OKX csproj references Core + Http only. Dependency direction is correct.
- [x] `IOkxHttpClient` and `OkxHttpClient` are both `internal`. No previously-internal type made
      public.
- [x] No new methods added to `IMarketDataService`, `ITradingService`, `IAccountService`, or
      `IExchangeClient`.
- [x] No new exchange client composer in this diff (TASK-014 scope is the HTTP wrapper only).
- [x] No DeltaMapper mapping profiles introduced; this diff contains no DTO→model mapping.
- [x] POST method present — retry applicability: retry is GET-only in `ExchangeResiliencePipeline`
      (enforced upstream); POST in this file does not re-enable retry.
- [x] Signing is entirely in `OkxSigningHandler` (delegating handler). Client only calls
      `OkxSigningRequest.MarkSigned` — no signing leakage.
- [x] `InternalsVisibleTo` for `DynamicProxyGenAssembly2` is present in the csproj (enables
      NSubstitute mocking of `IOkxHttpClient` in tests) — justified and correctly scoped.
- [x] No global state or static mutable fields introduced. `JsonOptions` is a `static readonly`
      immutable value-type — safe.
- [x] Aggregation / DI package is not referenced by this diff. Package-level coupling invariant
      (Invariant 10) is not violated.
- [x] Build: `dotnet build` succeeds with 0 warnings, 0 errors (`TreatWarningsAsErrors` setting
      respected).

---

## Summary

- PASS: Dependency direction — OKX csproj → Core + Http only; no upward or sideways reference.
- PASS: Visibility — `IOkxHttpClient` and `OkxHttpClient` are both `internal`; no public surface added.
- PASS: Sign-consistency invariant — GET/DELETE sign via `BuildUrl` → `PathAndQuery`; POST signs the verbatim `StringContent` body. The handler reads `RequestUri.PathAndQuery` (not the dict) and `Content.ReadAsStringAsync` (same bytes). No desync path exists under correct configuration.
- PASS: Query param ordering — same dict instance iterated once to build the string; handler reads the built string, not the dict. Ordering cannot desync.
- PASS: Trailing "?" edge — `BuildUrl` only appends `?` when query is non-empty.
- PASS: Separation of concerns — no signing leakage; all crypto in `OkxSigningHandler`.
- PASS: ADR-001 ctor layering — `OkxHttpClient(HttpClient)` only.
- PASS: Bybit pattern conformance — structural match; OKX-specific references correct.
- PASS: Build — 0 warnings, 0 errors.
- CONCERN: Endpoint without leading slash + wrong BaseAddress can desync signing — confidence 35, non-blocking, config concern owned by TASK-013 not this file.
- CONCERN: `Dictionary<string, string>` POST body — structural limitation inherited from the Bybit pattern; blocks nested OKX V5 payloads. Confidence 60, non-blocking. Worth flagging at next milestone boundary before the pattern copies to a 4th exchange.

---

VERDICT: APPROVED
Overall confidence: 95
