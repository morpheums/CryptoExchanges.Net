# Architect Review — TASK-021
**VERDICT**: APPROVED
**Confidence**: 97

## Findings

### Finding: Sign-consistency invariant — correctly upheld
- **Severity**: N/A (PASS)
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:96-118`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `BuildUrl` produces `"{endpoint}?{query}"` as a relative URI string. With a host-only `BaseAddress` (`https://api.bitget.com`, no trailing path), .NET resolves this so that `RequestUri.AbsolutePath` = the endpoint (e.g. `/api/v2/spot/market/tickers`) and `RequestUri.Query` = `?symbol=BTCUSDT_SPBL`. The signing handler reads `.AbsolutePath` and `.Query.TrimStart('?')` separately, passing them to `BitgetSignatureService.BuildPrehash(timestamp, method, requestPath, queryString, body)`, which re-inserts `?` only when `queryString.Length > 0`. The round-trip is byte-consistent: the query string the client encodes via `Uri.EscapeDataString` is the same string the handler signs. No gap in the invariant within these files.

### Finding: BaseAddress path-suffix risk — cross-task dependency, not a blocker on these files
- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:17-22` (XML doc), `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs:9`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence 65 < 80)
- **Issue**: The sign-consistency invariant requires `BaseAddress` to be host-root-only. `BitgetOptions.BaseUrl` defaults to `https://api.bitget.com` (correct). However, the composer (not yet implemented — a later task) will assign `BaseAddress` from `options.BaseUrl`. If an operator configures `BaseUrl` with a path suffix (e.g. `https://api.bitget.com/api/v2`), `RequestUri.AbsolutePath` would include the prefix and break the prehash. The class doc correctly documents the host-only requirement, but the enforcement is entirely on the yet-to-be-written composer. The risk is real but out of scope for this task.
- **Fix**: When the composer is implemented, validate `options.BaseUrl` at construction: `new Uri(options.BaseUrl).PathAndQuery` must be `/`. A guard like `if (uri.AbsolutePath != "/") throw new BitgetConfigurationException(...)` in the composer would make this invariant self-enforcing. No change required in these two files.
- **Pattern reference**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs:9` (default is correct); composer pattern TBD.

### Finding: Layering — no violations
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:1-5`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `using` statements reference only `System.Net.Http.Json`, `System.Text`, and `CryptoExchanges.Net.Bitget.Resilience` (same exchange assembly). No references to `CryptoExchanges.Net.Http`, `CryptoExchanges.Net.Core`, or any DI/aggregation package. `IBitgetHttpClient` has no `using` directives beyond the implicit namespace — correct for an internal interface file. No cross-layer or cross-exchange pollution.

### Finding: `internal` visibility on both interface and implementation
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs:4`, `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:30`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `IBitgetHttpClient` is `internal interface` and `BitgetHttpClient` is `internal sealed class`. Neither leaks to the public surface of the assembly. Consistent with invariant 3 (exchange client internals stay internal) and the OKX reference.

### Finding: Separation of concerns — signing stays in handler, client only marks
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:46,52,79,85,92`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Every signed request sets the marker via `BitgetSigningRequest.MarkSigned(request)` and no more. No inline HMAC computation, no secret reading, no header manipulation. The client is correctly decoupled from the signing logic; all signing occurs in `BitgetSigningHandler`.

### Finding: Content-Type set correctly at construction, not in signing handler
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:77`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `PostJsonAsync` constructs `new StringContent(json, Encoding.UTF8, "application/json")`, which sets `Content-Type: application/json; charset=utf-8` at the point of content creation. This confirms the TASK-019 CONCERN (non-blocking): `BitgetSigningHandler:62` redundantly overwrites `Content-Type` on every signing attempt. That redundancy originates in the signing handler (TASK-019 scope) and is irrelevant to these files — the wire result is correct. No action needed in this task.

### Finding: OKX adaptation — comment updated, runtime behavior unchanged
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:15-28`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The XML doc correctly describes the Bitget-specific delta vs. OKX: OKX signs `PathAndQuery` as one string; Bitget's signing handler reads `AbsolutePath` and `Query` separately. The doc communicates the invariant accurately. The runtime code (`BuildUrl`, `BuildQueryString`) is identical to `OkxHttpClient` — the delta is entirely in the prehash interpretation inside the signing handler, not here. No divergence from the OKX pattern introduced where none should exist.

### Finding: LEAN comment assessment — class doc is long but load-bearing
- **Severity**: LOW
- **Confidence**: 80
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:7-29`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — judgment call)
- **Issue**: The class XML doc is 22 lines covering three paragraphs. The ADR-001 LEAN convention targets minimal comments that explain why, not what. The first paragraph is standard boilerplate (same across all exchange clients). The second paragraph documents the sign-consistency invariant — this is genuinely load-bearing: the relationship between client URI construction and signing handler prehash is a non-obvious invariant that would cause a silent signing failure if a future maintainer misunderstood it. The third paragraph reiterates body verbatim. The second paragraph justifies the length; the first and third could be trimmed without losing information. This is a quality preference, not a correctness issue.
- **Fix**: Optional: remove the first paragraph (duplicates the OKX pattern doc exactly), shorten the third to one sentence. Net result: ~15 lines instead of 22. Non-blocking.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs:7-27` (same comment length — the pattern itself is verbose; trimming both in a follow-up pass would be consistent).

### Finding: `parameters ?? []` in PostAsync(Dictionary) — correct null-coalescing
- **Severity**: N/A (PASS)
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:59`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Serializing `null` parameters to `{}` is correct for Bitget POST endpoints that expect a JSON object body. The `[]` collection expression (C# 12 empty collection initializer resolving to `Dictionary<string,string>` via context) is idiomatic and matches the OKX reference.

## Summary

- PASS: Sign-consistency invariant — `BuildUrl` produces a URI whose `AbsolutePath` and `Query` are exactly the separate `requestPath`/`queryString` that `BitgetSigningHandler.BuildPrehash` signs. Round-trip is byte-consistent.
- PASS: Layering — no Core, Http, DI, or cross-exchange references; only same-assembly `Resilience` namespace.
- PASS: Visibility — both `IBitgetHttpClient` and `BitgetHttpClient` are `internal`; no public surface added.
- PASS: Separation of concerns — client only calls `MarkSigned`; no inline signing or secret handling.
- PASS: OKX adaptation — runtime behavior identical; doc correctly describes the Bitget-specific `AbsolutePath`/`Query` split vs. OKX's `PathAndQuery`.
- PASS: Content-Type set correctly at `StringContent` construction (confirms TASK-019 CONCERN redundancy, no action needed here).
- CONCERN: BaseAddress path-suffix risk — composer (later task) must validate `BaseUrl` is host-root-only; not a blocker on these files. (confidence: 65/100, non-blocking)
- CONCERN: Class XML doc length — first and third paragraphs could be trimmed; second is load-bearing. (confidence: 80/100, non-blocking — quality preference)

## Final Verdict
**APPROVED** — No blocking findings. Both files are a faithful and correctly adapted clone of the OKX HTTP wrapper pattern, with the sign-consistency invariant properly upheld within their scope. Two non-blocking CONCERNs are noted for the composer task and a documentation trim opportunity.
