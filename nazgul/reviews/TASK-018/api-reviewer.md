# API Review — TASK-018: BitgetSignatureService + BitgetSigningRequest

**VERDICT**: APPROVED
**Overall confidence**: 92/100
**Blocking items**: 0
**Non-blocking concerns**: 2

---

## Scope Confirmation

Review is limited to:
- `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs`
- `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs`

Both are new files; no existing files were modified.

---

## Findings

### Finding: Visibility — no public API leakage
- **Severity**: N/A — PASS
- **Confidence**: 100
- **Files**: Both files
- **Category**: API Design / Compatibility
- **Verdict**: PASS
- `BitgetSignatureService` is `internal sealed`. `BitgetSigningRequest` is `internal static`. Public static methods (`BuildPrehash`, `FormatTimestamp`, `MarkSigned`, `IsSigned`) on internal types are assembly-bounded; they cannot be resolved by external consumers and do not widen the NuGet package surface. No `[assembly: InternalsVisibleTo]` is added here.

---

### Finding: ISignatureService contract — correctly implemented
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: `BitgetSignatureService.cs:16-17`
- **Category**: API Design
- **Verdict**: PASS
- `Sign(string payload)` is `<inheritdoc />` and delegates to `HmacSignature.Compute(_secretKey, payload, SignatureEncoding.Base64)` — identical delegation pattern to `OkxSignatureService.cs:15-16`. No re-implemented crypto.

---

### Finding: BitgetSigningRequest mirrors OkxSigningRequest exactly
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: `BitgetSigningRequest.cs:1-22`
- **Category**: API Design
- **Verdict**: PASS
- `HttpRequestOptionsKey<bool>` keyed `"bitget.signed"` (vs `"okx.signed"`). `MarkSigned`/`IsSigned` signatures, null guard pattern, and `TryGetValue(SignedKey, out var v) && v` idiom are byte-for-byte consistent with `OkxSigningRequest.cs`.

---

### Finding: Prehash param ordering — extra `queryString` param is sensibly placed
- **Severity**: N/A — PASS
- **Confidence**: 95
- **File**: `BitgetSignatureService.cs:31`
- **Category**: API Design
- **Verdict**: PASS
- Bitget's `BuildPrehash(timestamp, method, requestPath, queryString, body)` inserts `queryString` between `requestPath` and `body`, which correctly reflects the wire order of the assembled string. Naming is clear and follows the existing parameter naming convention from OKX.

---

### Finding: Secret guard — matches established pattern
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: `BitgetSignatureService.cs:47-51`
- **Category**: API Design
- **Verdict**: PASS
- `InitializeSecretKey` with `ThrowIfNullOrWhiteSpace` mirrors `OkxSignatureService.cs:52-55` and `BinanceSignatureService.cs:37-41`.

---

### Finding: Timestamp format — epoch-ms correctly differs from OKX ISO-8601
- **Severity**: N/A — PASS
- **Confidence**: 98
- **File**: `BitgetSignatureService.cs:38-39`
- **Category**: API Design
- **Verdict**: PASS
- `ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)` matches Bitget's documented epoch-millisecond expectation. This intentionally diverges from OKX's ISO-8601 `FormatTimestamp` and the class-level doc calls out the distinction.

---

### Finding: BuildPrehash — missing per-param XML doc (non-blocking)
- **Severity**: LOW
- **Confidence**: 85
- **File**: `BitgetSignatureService.cs:19-35`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking)
- OKX's `BuildPrehash` includes full `<param>` and `<returns>` and `<exception>` XML doc tags for each parameter (OkxSignatureService.cs:19-33). Bitget's summary doc describes the prehash shape in prose but omits individual `<param>` blocks. Because this type is `internal sealed`, omission has no end-user impact, and the existing prose doc is serviceable — but for consistency with the OKX sibling pattern (ADR-001 conv 7 references lean-but-consistent comments), adding the param blocks would align the codebase.
- **Recommendation**: Add `<param name="queryString">`, `<param name="body">`, `<returns>`, and `<exception>` entries matching the OKX pattern. Non-blocking since the type is internal and tests cover behavior.

---

### Finding: FormatTimestamp — no XML doc param/returns tags (non-blocking)
- **Severity**: LOW
- **Confidence**: 75
- **File**: `BitgetSignatureService.cs:43-39`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking, confidence < 80)
- OKX's `FormatTimestamp` has a full `<param>` and `<returns>` block (OkxSignatureService.cs:43-50). Bitget's is a single-line summary only. Same rationale as above — internal type, behavioral correctness unaffected, but consistency is lost. The lower confidence (75) reflects that single-line summaries on simple helpers are defensible under "lean comments."

---

## Checklist Summary

| Check | Result |
|---|---|
| Both types are `internal` — no public API leakage | PASS |
| `internal sealed` / `internal static` modifiers correct | PASS |
| `public static` helpers on internal type are assembly-bounded | PASS |
| `ISignatureService.Sign` correctly implemented with `<inheritdoc />` | PASS |
| `BuildPrehash` param ordering/naming consistent with task spec | PASS |
| `queryString` guard is `ThrowIfNull` (not `ThrowIfNullOrWhiteSpace`) — correct, query may be empty | PASS |
| `body` guard is `ThrowIfNull` — correct, body may be empty | PASS |
| `FormatTimestamp` uses epoch-ms, not ISO-8601 | PASS |
| `BitgetSigningRequest` mirrors `OkxSigningRequest` shape | PASS |
| `HttpRequestOptionsKey` key namespaced to `"bitget.signed"` | PASS |
| No `InternalsVisibleTo` added | PASS |
| No existing interface/model modified | PASS |
| No public API surface change | PASS |
| XML doc quality — class-level summary adequate | PASS |
| XML doc quality — param-level detail matches OKX sibling | CONCERN (non-blocking) |

---

## Final Verdict

**APPROVED** — Both files are correctly scoped as `internal`, implement the shared `ISignatureService` contract faithfully, match the OKX/Bybit sibling shape, and introduce no public API surface changes. The two low-severity concerns (missing per-param XML doc tags on internal helpers) are non-blocking and can be addressed in a follow-up polish pass.
