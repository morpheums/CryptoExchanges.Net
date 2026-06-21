---
verdict: CHANGES_REQUESTED
---
# API Review — TASK-057

## Verdict
CHANGES_REQUESTED

## Summary
All four new source types are correctly `internal`, and both interfaces are correctly implemented. One blocking issue: `KucoinSigningHandler` takes `KucoinSignatureService` (the concrete type) as its `signatureService` parameter instead of `ISignatureService`, deviating from the established OKX, Bybit, and Bitget pattern and coupling the handler to the concrete class. This needs to be resolved before merging.

## API Surface Check
- [x] All types are internal (no public leakage): PASS
- [x] ISignatureService implemented correctly: PASS
- [x] IExchangeErrorTranslator implemented correctly: PASS
- [x] Static method signatures clean: PASS
- [x] Namespace consistency: PASS
- [ ] Constructor parameter types consistent with OKX pattern: FAIL — `KucoinSigningHandler` takes `KucoinSignatureService` not `ISignatureService`

## Findings

### Finding: KucoinSigningHandler takes concrete `KucoinSignatureService` instead of `ISignatureService`
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:209-210`
- **Category**: API Design
- **Verdict**: REJECT (blocking)
- **Issue**: The handler's primary constructor parameter is typed as `KucoinSignatureService signatureService` rather than `ISignatureService signatureService`. Every other exchange signing handler (`OkxSigningHandler`, `BybitSigningHandler`, `BitgetSigningHandler`) uses `ISignatureService` for this parameter. This introduces a concrete-type dependency that prevents substitution (e.g. in unit tests with a mock or stub) and deviates from the established pattern without a justifying comment.
- **Root cause**: `KucoinSignatureService` carries an additional method `SignPassphrase(string)` that is not on `ISignatureService`. The handler calls `signatureService.SignPassphrase(passphrase)` at line 258, which forces the concrete type to be visible at the call site.
- **Fix**: Extend `ISignatureService` with a default interface method (DIM) `SignPassphrase` that throws `NotSupportedException` (keeping it non-breaking for existing implementors), then use `ISignatureService` in the constructor. Alternatively — and more conservatively — define a narrower `IPassphraseSignatureService : ISignatureService` that adds `SignPassphrase(string passphrase)`, have `KucoinSignatureService` implement it, and type the handler parameter as `IPassphraseSignatureService`. Either approach restores interface-based injection while accommodating the KuCoin-specific passphrase signing requirement.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:19` — `ISignatureService signatureService`; `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:19` — `ISignatureService signatureService`

---

### Finding: `SignPassphrase` is a KuCoin-specific method that extends beyond `ISignatureService` with no interface contract
- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs:35-39`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking on its own; resolved by the REJECT fix above)
- **Issue**: `SignPassphrase` is a KuCoin-specific capability that has no home on `ISignatureService`. This is by design (OKX, Bybit, Bitget do not need it), but leaving it as an uncontracted method on the concrete class means the handler is implicitly unable to use the abstraction layer. The method itself is well-formed and correctly validated.
- **Fix**: Address via the interface strategy described in the REJECT finding above (DIM or a new narrower interface).

---

### Finding: `ISignatureService.Sign` parameter name mismatch — minor doc inconsistency
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs:23`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The interface defines `string Sign(string payload)` but the implementation's `<inheritdoc/>` inherits from a method named `Sign(string prehash)`. The parameter name `prehash` vs `payload` is not a compile error and not visible to callers since this is `internal`, but it is a minor doc inconsistency that is visible in VS tooling when browsing the inherited documentation. OKX also names the parameter `prehash` — so this is consistent within the exchange implementations but inconsistent with the interface declaration.
- **Fix**: Rename the local parameter to `payload` to match the interface definition, or update the interface. Low priority.

---

### Finding: `public static` methods on `internal` classes — pattern is consistent, no leakage
- **Severity**: INFO
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs:59,75`; `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningRequest.cs:289,296`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `public static` on an `internal` type is the correct C# pattern: the accessibility of a member is bounded by the accessibility of its declaring type, so these are effectively `internal static`. Consistent with `OkxSignatureService`, `OkxSigningRequest`, and `BitgetSignatureService`.

---

### Finding: `KucoinErrorTranslator.Translate` — `ArgumentNullException.ThrowIfNull(response)` guards `response` but not `body`
- **Severity**: LOW
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinErrorTranslator.cs:109-111`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `body` is not explicitly null-guarded before `Parse(body)`. `Parse` handles `string.IsNullOrWhiteSpace(body)` which returns `(null, null)` for null input, so no exception surfaces at runtime. However the interface contract `Translate(HttpResponseMessage response, string body)` implies `body` is a non-null `string`. The OKX counterpart has the same omission. Low risk but minor inconsistency with the guard style used elsewhere in this task (`ArgumentNullException.ThrowIfNull` is used throughout).
- **Fix**: Add `ArgumentNullException.ThrowIfNull(body);` after the `response` guard, or explicitly note in the method comment that null body is treated as empty. Low priority.

---

## Summary Table

| Item | Result | Note |
|---|---|---|
| All source types are `internal` | PASS | `KucoinSignatureService`, `KucoinSigningHandler`, `KucoinSigningRequest`, `KucoinErrorTranslator` are all `internal sealed` |
| `ISignatureService` correctly implemented | PASS | `Sign(string)` member present and returns `string`; `KucoinSignatureService` also adds `SignPassphrase` |
| `IExchangeErrorTranslator` correctly implemented | PASS | `Translate(HttpResponseMessage, string)` signature matches interface |
| `BuildPrehash` / `FormatTimestamp` static signatures | PASS | Same shape as OKX counterparts; `FormatTimestamp` correctly uses Unix-ms not ISO-8601 |
| Namespace consistency | PASS | `Auth/` → `CryptoExchanges.Net.Kucoin.Auth`; `Resilience/` → `CryptoExchanges.Net.Kucoin.Resilience` — matches OKX/Bybit/Bitget layout |
| `KucoinSigningRequest` pattern | PASS | `MarkSigned`/`IsSigned` signatures and option key naming (`"kucoin.signed"`) match `OkxSigningRequest` pattern |
| Handler constructor parameter type | REJECT | `KucoinSignatureService` (concrete) used instead of `ISignatureService` (interface); see blocking finding above |
| LR-004 compliance (array null+length guards) | N/A | No array parameters in these files |
