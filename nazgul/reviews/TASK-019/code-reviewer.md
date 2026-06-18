# Code Review — TASK-019

**Verdict**: APPROVED
**Confidence**: 95

## Findings

### PASS [confidence 98] [non-blocking] BitgetSigningHandler.cs:34-38 — Unsigned pass-through correct
`BitgetSigningRequest.IsSigned` guards the sign path; unsigned requests flow directly to `base.SendAsync`. Pattern matches `OkxSigningHandler.cs:28-31` exactly.

### PASS [confidence 98] [non-blocking] BitgetSigningHandler.cs:76-83 — Strip-then-add removes all four headers
All four `ACCESS-*` headers are stripped before adding fresh copies, satisfying AC-2 (exactly one set of headers on retry). Order (Remove all, then Add all) prevents partial-header state if an exception were thrown mid-add.

### PASS [confidence 97] [non-blocking] BitgetSigningHandler.cs:53-54 — Path/query split matches BuildPrehash contract
`AbsolutePath` gives the decoded path without the query; `Query.TrimStart('?')` gives the raw query without the leading `?`. `BuildPrehash` (BitgetSignatureService.cs:33) re-inserts the `?` only when `queryString.Length > 0`, making the concatenation `timestamp+METHOD+path+?query+body` exactly what Bitget expects. Empty-query edge case is correct: `TrimStart('?')` on `""` returns `""`, and `BuildPrehash` then omits the `?` entirely.

### PASS [confidence 96] [non-blocking] BitgetSigningHandler.cs:57-63 — Content-Type set only when content present
`Content-Type: application/json` is set via `request.Content.Headers.ContentType` only inside the `request.Content is not null` branch. GET/DELETE requests with no body do not receive a Content-Type header.

### PASS [confidence 98] [non-blocking] BitgetSigningHandler.cs:29,31,61 — ConfigureAwait(false) on all awaits
All three `await` sites carry `.ConfigureAwait(false)`: `ResignAsync` call, `base.SendAsync`, and `ReadAsStringAsync`.

### PASS [confidence 97] [non-blocking] BitgetSigningHandler.cs:18-19 — Primary ctor, ISignatureService interface
Constructor takes `ISignatureService` (Core interface), not the concrete `BitgetSignatureService`, keeping signing swappable as mandated by TASK-019 impl notes (REF-002 / OKX pattern).

### PASS [confidence 99] [non-blocking] Build — Zero warnings/errors
`dotnet build CryptoExchanges.Net.sln` reports `Build succeeded. 0 Warning(s), 0 Error(s)` with `TreatWarningsAsErrors=true` active.

### PASS [confidence 96] [non-blocking] BitgetSigningHandler.cs:7-23 — XML doc coverage
Class `<summary>` and all four `<param>` tags are present. `SendAsync` uses `/// <inheritdoc />`. No redundant doc on the private `ResignAsync`. Matches the LEAN comment mandate.

### CONCERN [LOW] [confidence 72] [non-blocking] BitgetSigningHandler.cs:53 — RequestUri! null-forgiveness safety
`request.RequestUri!` suppresses the nullable warning. For well-formed `HttpRequestMessage` instances this is safe: `HttpClient` populates `RequestUri` before the handler chain runs, and the outgoing message always has a URI. The null-forgiveness is safe in practice but there is no comment justifying the suppression (cf. `BinanceSigningHandler.cs:49` which uses the same pattern silently). Confidence is below 80 so this is non-blocking; a one-line inline comment noting the invariant ("HttpClient guarantees RequestUri is non-null at this point in the pipeline") would be consistent with the project's pragma-justification convention.

### CONCERN [LOW] [confidence 65] [non-blocking] BitgetSigningHandler.cs:45,49 — Guards fire inside private method, not at ctor
`apiKey` and `passphrase` are checked for null/empty inside the private `ResignAsync`, not at construction time. This means an invalid configuration surfaces only on the first signed request, not at DI composition. `OkxSigningHandler.cs` follows the same deferred pattern, so this is consistent. Flagged as a concern for awareness — the task acceptance criteria explicitly calls this out as the expected behaviour (AC-3: "Missing passphrase on a signed request fails fast").

## Summary

- PASS: Re-sign per attempt — Strip-then-add of all four `ACCESS-*` headers confirmed; `ACCESS-TIMESTAMP` and `ACCESS-SIGN` are recomputed from a fresh `UtcNow + timeOffset()` epoch-ms on every `SendAsync` invocation.
- PASS: Path/query split — `AbsolutePath` + `Query.TrimStart('?')` feeds `BuildPrehash` correctly; empty-query and multi-param query edge cases are handled without any code change needed.
- PASS: Signature via injected service — `ISignatureService.Sign` used; no inline crypto.
- PASS: Content-Type guarded — set only when `request.Content is not null`.
- PASS: ConfigureAwait(false) — present on all three await sites.
- PASS: Build — 0 warnings, 0 errors under `TreatWarningsAsErrors=true`.
- PASS: XML docs — complete and LEAN; `<inheritdoc />` on the override.
- CONCERN: `RequestUri!` null-forgiveness (confidence: 72/100, non-blocking) — safe in practice, but a brief inline justification comment would match the codebase convention.
- CONCERN: Guards deferred to `ResignAsync` rather than ctor (confidence: 65/100, non-blocking) — consistent with `OkxSigningHandler`; AC-3 accepts this behaviour.
