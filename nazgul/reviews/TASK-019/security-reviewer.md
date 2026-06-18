# Security Review — TASK-019

**Verdict**: APPROVED
**Confidence**: 98

## Findings

### PASS [confidence 99] [non-blocking] BitgetSigningHandler.cs:53-66 — Prehash/wire byte consistency confirmed

The handler passes `request.RequestUri!.AbsolutePath` (line 53) and `request.RequestUri.Query.TrimStart('?')` (line 54) separately to `BitgetSignatureService.BuildPrehash`. `BuildPrehash` (Auth/BitgetSignatureService.cs:33) re-inserts the `?` only when `queryString.Length > 0`, reconstructing exactly `AbsolutePath + ?query` — which is byte-for-byte what Bitget receives on the wire. No signature/payload mismatch.

### PASS [confidence 99] [non-blocking] BitgetSigningHandler.cs:47-48 — Fresh timestamp per attempt

`DateTimeOffset.UtcNow.AddMilliseconds(timeOffset())` is evaluated on every call to `ResignAsync`. Each retry gets a fresh epoch-ms timestamp, preventing timestamp-expiry rejections. Pattern matches OkxSigningHandler.cs:47.

### PASS [confidence 99] [non-blocking] BitgetSigningHandler.cs:70-77 — Strip-then-re-add all four ACCESS-* headers on retry

All four `ACCESS-KEY`, `ACCESS-SIGN`, `ACCESS-TIMESTAMP`, `ACCESS-PASSPHRASE` are removed before re-adding (lines 70-77). A retried request carries exactly one set of ACCESS-* headers with a fresh timestamp and signature. Pattern matches OkxSigningHandler.cs:68-75.

### PASS [confidence 99] [non-blocking] BitgetSigningHandler.cs:19,66 — No inline crypto; secret never in handler

The constructor takes `ISignatureService` (Core interface at line 19), not the concrete `BitgetSignatureService`. Signing is fully delegated to `signatureService.Sign(prehash)` at line 66. The `secretKey` lives exclusively inside `BitgetSignatureService._secretKey` (Auth/BitgetSignatureService.cs:13) and is never accessible in this handler.

### PASS [confidence 99] [non-blocking] BitgetSigningHandler.cs:39-44 — Fail-fast guards fire before base.SendAsync

`string.IsNullOrEmpty(apiKey)` and `string.IsNullOrEmpty(passphrase)` are checked at lines 39-44 inside `ResignAsync`, which is awaited at line 29 before `base.SendAsync` at line 31. No unsigned-but-intended-signed request can leave the client. Exception messages (lines 41, 44) contain only descriptive text — no credential values are interpolated.

### PASS [confidence 99] [non-blocking] BitgetSigningHandler.cs:28-31 — Unsigned pass-through has zero credential headers

The `if (BitgetSigningRequest.IsSigned(request))` guard at line 28 is the only path that calls `ResignAsync`. Unsigned (public) requests skip `ResignAsync` entirely and flow directly to `base.SendAsync` with no ACCESS-* headers added. No accidental credential leakage on public calls.

### PASS [confidence 98] [non-blocking] BitgetSigningHandler.cs:58-62 — Content-Type set only when content is present; DELETE body correctly excluded

`request.Content.Headers.ContentType` is set at line 62 only inside the `request.Content is not null` branch for POST/PUT. DELETE is absent from the method condition (line 58), so `body` remains `""` for DELETE — consistent with Bitget's prehash spec (Auth/BitgetSignatureService.cs:9: "body is empty for GET/DELETE") and with how `BitgetHttpClient` constructs DELETE requests (no content attached).

### PASS [confidence 97] [non-blocking] BitgetSigningHandler.cs — No credential logging or serialization

The handler contains no logging statements, no `ToString()` override, no `JsonSerializer` calls. `apiKey` and `passphrase` flow only into the `ACCESS-KEY` and `ACCESS-PASSPHRASE` request headers — which is the correct and only intended transmission path per Bitget's API contract.

## Summary

- PASS: Prehash/wire consistency — `AbsolutePath` + stripped query passed separately to `BuildPrehash`; reconstructed `?query` is byte-for-byte identical to the wire URL. No signature mismatch.
- PASS: Re-sign per attempt — fresh `UtcNow + timeOffset()` on every `ResignAsync` call; all four ACCESS-* headers stripped then re-added.
- PASS: No inline crypto — `ISignatureService` interface used; secret key never surfaces in this handler.
- PASS: Fail-fast guards — `string.IsNullOrEmpty` checks on `apiKey` and `passphrase` fire before `base.SendAsync`; exception messages contain no credential values.
- PASS: Unsigned pass-through — `IsSigned` guard ensures public requests receive no ACCESS-* headers.
- PASS: Content-Type safety — set only when content is present; DELETE correctly excluded from body extraction.
- PASS: No credential leakage — no logging, no `ToString`, no serialization of `apiKey` or `passphrase`.
