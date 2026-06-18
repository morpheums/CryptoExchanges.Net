# API Review — TASK-019

**Verdict**: APPROVED
**Confidence**: 97

## Findings

PASS [confidence 99] [non-blocking] `BitgetSigningHandler.cs:18` — `internal sealed class` — Type is correctly scoped as `internal sealed`, placing it entirely within the `CryptoExchanges.Net.Bitget` assembly. No public API surface is widened; no new public types or members are introduced into the NuGet package contract.

PASS [confidence 99] [non-blocking] `BitgetSigningHandler.cs:18-19` — Constructor shape — Ctor parameters `(string apiKey, string passphrase, ISignatureService signatureService, Func<long> timeOffset)` are identical in order and type to `OkxSigningHandler.cs:19`. Shape parity between sibling exchanges is exact. Depends on Core `ISignatureService` abstraction (not the concrete `BitgetSignatureService`), consistent with the REF-002 swappable-signer seam.

PASS [confidence 98] [non-blocking] `BitgetSigningHandler.cs:7-17` — XML documentation — Class-level `<summary>` is present and clearly describes positioning relative to the retry strategy. All four primary ctor parameters have `<param>` tags. `SendAsync` override carries `<inheritdoc />`. Documentation density matches `OkxSigningHandler.cs:7-17`.

PASS [confidence 97] [non-blocking] `BitgetSigningHandler.cs:28` — Unsigned pass-through — `BitgetSigningRequest.IsSigned(request)` guard mirrors `OkxSigningRequest.IsSigned` in OKX. Public requests pass through without any auth headers being set. Correct.

PASS [confidence 97] [non-blocking] `BitgetSigningHandler.cs:53-54` — Path/query split — Bitget's `BuildPrehash` takes `requestPath` and `queryString` separately (re-inserts `?` only when non-empty). The handler correctly splits `RequestUri.AbsolutePath` and `RequestUri.Query.TrimStart('?')` rather than using `PathAndQuery` as OKX does. This is a documented, intentional divergence from the OKX pattern and is correct for Bitget's signing convention.

PASS [confidence 95] [non-blocking] `BitgetSigningHandler.cs:62` — `Content-Type` re-stamping — On POST/PUT, `request.Content.Headers.ContentType` is set to `application/json` after reading the body. OKX does not do this (the OKX handler has no equivalent line). This is a Bitget-specific requirement from the task spec ("Content-Type: application/json set on the StringContent for POST/PUT"). Setting it on `Content.Headers` (not `request.Headers`) is the correct location for a content header. Non-blocking — Bitget-specific behaviour, not a pattern deviation.

PASS [confidence 96] [non-blocking] `BitgetSigningHandler.cs:39-44` — Fail-fast guards — Null/empty `apiKey` and `passphrase` throw `InvalidOperationException` with clear messages naming the header, mirroring the OKX pattern at `OkxSigningHandler.cs:39-44`. Error messages correctly reference the Bitget-specific header names (`ACCESS-KEY`, `ACCESS-PASSPHRASE`).

PASS [confidence 98] [non-blocking] `BitgetSigningHandler.cs:70-77` — Header strip-and-re-add — All four `ACCESS-*` headers are removed then re-added in each `ResignAsync` call, ensuring exactly one set of signing headers after retry. Order of strip/add matches OKX. Correct.

PASS [confidence 99] [non-blocking] `BitgetSigningHandler.cs` — Backwards compatibility — This is a new file. No existing public or internal type is modified. No interface or record member is added, removed, or renamed. Blast radius is additive only.

## Summary

- PASS: `internal sealed` scoping — no public API surface widened
- PASS: Constructor shape — exact parity with `OkxSigningHandler` (same parameter order and types); `ISignatureService` abstraction preserved
- PASS: XML docs — class summary + four `<param>` tags + `<inheritdoc />` on `SendAsync`
- PASS: Unsigned pass-through — `IsSigned` guard correctly routes public requests around signing
- PASS: Path/query split — correct Bitget-specific divergence from OKX `PathAndQuery` pattern; well-commented
- PASS: `Content-Type` re-stamp on POST/PUT — Bitget requirement, correctly placed on `Content.Headers`
- PASS: Fail-fast guards — match OKX pattern; header names in messages are Bitget-correct
- PASS: Strip-and-re-add on retry — exactly one set of ACCESS-* headers per attempt
- PASS: Backwards compatibility — new file, purely additive
