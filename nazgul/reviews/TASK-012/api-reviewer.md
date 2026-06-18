VERDICT: APPROVED

## Review: OkxSigningHandler (TASK-012)

**File reviewed**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs`

---

### Finding 1: No public/protected API exposure — PASS
- **Severity**: N/A
- **Confidence**: 100
- **File**: `OkxSigningHandler.cs:17-19`
- **Category**: API Design
- **Verdict**: PASS
- The class is `internal sealed`. The only non-private member is `protected override SendAsync`, which is the standard override of `DelegatingHandler.SendAsync`. No new protected virtual members, no public constructor, no public properties. Adds zero NuGet surface.

---

### Finding 2: Constructor shape is consistent with Bybit sibling — PASS
- **Severity**: N/A
- **Confidence**: 99
- **File**: `OkxSigningHandler.cs:17-18`
- **Category**: API Design
- **Verdict**: PASS
- Bybit: `(string apiKey, BybitSignatureService, string recvWindow, Func<long> timeOffset)`.
  OKX: `(string apiKey, string passphrase, OkxSignatureService, Func<long> timeOffset)`.
  The OKX variant correctly adds `passphrase` (OKX-specific credential absent in Bybit) and drops `recvWindow` (OKX does not use a recvWindow header). The `Func<long> timeOffset` pattern for server-time drift correction is preserved exactly.

---

### Finding 3: Pluggability into requestFinalizerFactory contract — PASS
- **Severity**: N/A
- **Confidence**: 100
- **File**: `OkxSigningHandler.cs:22-31`
- **Category**: API Design
- **Verdict**: PASS
- The `requestFinalizerFactory` slot in `ApplyResiliencePipeline` (`ResilientHttpClientServiceCollectionExtensions.cs:66`) accepts `Func<IServiceProvider, DelegatingHandler>`. `OkxSigningHandler : DelegatingHandler` satisfies that contract directly. The `OkxSigningRequest.IsSigned` marker handshake (options key `"okx.signed"` set by `OkxHttpClient`, read by `SendAsync`) mirrors `BybitSigningRequest.IsSigned` exactly and is confined to the OKX namespace.

---

### Finding 4: Header strip-before-add idempotency on retry — PASS
- **Severity**: N/A
- **Confidence**: 100
- **File**: `OkxSigningHandler.cs:67-74`
- **Category**: API Design
- **Verdict**: PASS
- All four `OK-ACCESS-*` headers are removed before re-adding on each attempt. This matches the Bybit handler's strip pattern (`BybitSigningHandler.cs:59-64`) and prevents header duplication on Polly retries — the explicitly documented purpose of placing the signing handler inside the Polly retry boundary.

---

### Finding 5: Body read for DELETE not covered — CONCERN (non-blocking)
- **Severity**: LOW
- **Confidence**: 55
- **File**: `OkxSigningHandler.cs:56-60`
- **Category**: API Design
- **Verdict**: CONCERN
- **Issue**: The body-reading block fires only on `HttpMethod.Post || HttpMethod.Put`. OKX V5 has DELETE endpoints that accept a JSON body (e.g. `DELETE /api/v5/trade/order` sends the order ID in the body). If `OkxHttpClient.DeleteAsync` ever sends a body, the signing handler would sign `body = ""` while the wire body is non-empty, producing a signature mismatch.
- **Current risk**: Examining `OkxHttpClient.DeleteAsync` (line 67-76) shows it currently builds the request with `BuildUrl` (query string only) and sends no `Content`, so today's DELETE calls are safe. This is a forward-compatibility gap rather than a current bug.
- **Fix**: When OKX DELETE-with-body endpoints are introduced, extend the body-read condition to `|| request.Method == HttpMethod.Delete` and ensure `OkxHttpClient.DeleteAsync` supports a body parameter.
- **Pattern reference**: `BybitSigningHandler.cs:44` — Bybit only signs POST body; Bybit's DELETE is also query-only. The asymmetry is OKX-specific.

---

### Finding 6: `ArgumentNullException.ThrowIfNull(request)` guarding a nullable dereference — PASS
- **Severity**: N/A
- **Confidence**: 98
- **File**: `OkxSigningHandler.cs:25`
- **Category**: API Design
- **Verdict**: PASS
- `request.RequestUri!.PathAndQuery` (line 52) uses null-forgiving; the null-guard at line 25 covers the `request` null case. `RequestUri` can theoretically be null on a manually constructed message, but the `!` is acceptable here as `OkxHttpClient` always sets the URI. Consistent with the Bybit handler which also uses `request.RequestUri?.Query` with a null-coalesce rather than null-forgiving — minor style variance, not a functional issue.

---

### Finding 7: XML doc completeness — PASS
- **Severity**: N/A
- **Confidence**: 100
- **File**: `OkxSigningHandler.cs:5-16`
- **Category**: API Design
- **Verdict**: PASS
- Type-level `<summary>` is thorough and explains the retry-safe re-signing rationale, the four headers, and the deliberate pass-through for unsigned requests. All four primary constructor parameters have `<param>` docs. `ResignAsync` is private and appropriately uses inline comments rather than XML docs. `SendAsync` uses `<inheritdoc />` which is correct for a `protected override`.

---

### Finding 8: `string.IsNullOrEmpty` guards inside ResignAsync vs constructor — PASS
- **Severity**: N/A
- **Confidence**: 90
- **File**: `OkxSigningHandler.cs:38-43`
- **Category**: API Design
- **Verdict**: PASS
- The comment at line 37-38 correctly explains why the guards are here (belt-and-suspenders against a misconfigured composer) rather than only in the constructor. This is the same defensive reasoning applied in the Bybit DI path (`ServiceCollectionExtensions.cs:96-97` where a `PassThroughHandler` is returned if `SecretKey` is empty). Acceptable design; the OKX composer for DI (not yet implemented in this task) is expected to follow the same conditional pattern.

---

## Summary

- PASS: No public NuGet surface added — `internal sealed` with only `protected override SendAsync`.
- PASS: Constructor shape consistent with Bybit sibling — `passphrase` addition and `recvWindow` removal are OKX-correct.
- PASS: `requestFinalizerFactory` contract satisfied — `DelegatingHandler` subtype, `IsSigned` marker handshake mirrors Bybit.
- PASS: Retry idempotency — header strip-before-add on all four `OK-ACCESS-*` headers.
- PASS: XML doc completeness — type, all constructor params, `<inheritdoc />` on override.
- PASS: Credential null-guards in `ResignAsync` are intentional and correctly explained.
- CONCERN: DELETE body signing not covered (confidence: 55/100, non-blocking) — no current OKX DELETE endpoint sends a body, but the handler will need extending before any such endpoint is added.

## Final Verdict

APPROVED — All API-surface checks pass. The single CONCERN is a forward-compatibility note with confidence below the 80 blocking threshold and no current functional impact.
