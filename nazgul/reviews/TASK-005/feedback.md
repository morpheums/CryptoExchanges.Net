# Review Gate Feedback — TASK-005

**Task**: BybitHttpClient + IBybitHttpClient (internal HTTP wrapper, JSON-body POST)
**Commit**: 2a598c8
**Branch**: feat/m2-exchange-expansion
**Date**: 2026-06-17

## Aggregate Verdict: CHANGES_REQUESTED

| Reviewer | Verdict | Confidence | Blocking? |
|---|---|---|---|
| architect-reviewer | APPROVE | 97 | no |
| security-reviewer | APPROVE | 95 | no |
| api-reviewer | APPROVE | 93 | no |
| code-reviewer | REJECT | 97 | **yes** |

Gate policy: `require_all_approve=true`, `confidence_threshold=80`, `auto_approve_concerns=true`, `block_on_security_reject=true`.
The single REJECT is at confidence 97 (≥ 80) → CHANGES_REQUESTED. Security approved, so `block_on_security_reject` is not triggered.

---

## BLOCKING — must fix before re-review

### B1. Missing `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` guard on all three methods
- **Reviewer**: code-reviewer (REJECT, confidence 97, severity HIGH)
- **File**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs` — `GetAsync` (line 26), `PostAsync` (line 37), `DeleteAsync` (line 52)
- **Issue**: All three methods accept `string endpoint` with no entry guard. A null/whitespace value surfaces as a `UriFormatException` from inside `HttpRequestMessage` rather than a clean `ArgumentException` at the API surface. The project guard convention (`SymbolMapper.cs:76`, the signature services) requires `ArgumentException.ThrowIfNullOrWhiteSpace` on string parameters.
- **Fix**: Add `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);` as the first statement of each of the three methods.

> **Gate note (for orchestrator/implementer):** Verified by the gate — the mandated pattern reference `BinanceHttpClient` does NOT guard `endpoint` either, so the current code is faithful to its stated pattern; the deviation is from the broader project guard convention. The fix is trivial, additive, and strictly improves on the Binance precedent. Recommended to apply it here. Consider back-filling the same guard into `BinanceHttpClient` separately to keep the two HTTP wrappers consistent (out of scope for TASK-005).

---

## NON-BLOCKING (auto-approved, below threshold — informational only)

These are recorded but do not block the gate. The implementer may address them opportunistically.

### N1. Redundant `using var content` + `using var request { Content = content }` (double-dispose)
- code-reviewer, CONCERN, confidence 70. `HttpRequestMessage.Dispose()` already disposes its `Content`; the explicit `using var content` disposes a second time (idempotent in .NET). Matches the Binance reference exactly (`BinanceHttpClient.cs:53-54`). File: `BybitHttpClient.cs:44-45`.

### N2. Single `JsonOptions` instance used for both serialize and deserialize
- code-reviewer, CONCERN, confidence 55. `DefaultIgnoreCondition = WhenWritingNull` has no effect on the current `Dictionary<string,string>` POST body, but a future POST body type with nullable fields would have null fields silently omitted from the signed body — a potential signature-mismatch footgun. File: `BybitHttpClient.cs:18-23, 43`. Document the constraint or split serialize/deserialize options if/when a richer POST body type is introduced.

### N3. (architect / api / security) `GetStringAsync` omitted vs Binance
- Acceptable for TASK-005 (acceptance criteria require only Get/Post/Delete). api-reviewer flags it may be needed in TASK-006 depending on the klines array-of-arrays shape. Track, do not block.

### N4. (security) Endpoint passed unescaped; query values escaped via `Uri.EscapeDataString`
- security-reviewer, non-blocking. `endpoint` is caller-controlled internal API (not user input); query keys/values are escaped. Consistent with Binance. No injection vector for internal callers.

---

## Confirmed PASS (key correctness items)

- **POST body signing fidelity (the central deliberate delta)**: VERIFIED by code-reviewer (conf 99) and security-reviewer. `JsonSerializer.Serialize(...)` → `StringContent(Encoding.UTF8, "application/json")` is stored verbatim; `BybitSigningHandler.ResignAsync` reads it back via `request.Content.ReadAsStringAsync` (handler line 42) and signs that exact text. Nothing re-serializes or mutates the body between client and handler. `StringContent` is re-readable across retry attempts.
- GET/DELETE sign-string sourced from `RequestUri.Query` verbatim; insertion-order query enumeration is correct for Bybit V5 (no canonical sort required) — code-reviewer PASS.
- All awaits `.ConfigureAwait(false)`; `CancellationToken` forwarded to `SendAsync` and `ReadFromJsonAsync`.
- `IBybitHttpClient` is `internal` with `InternalsVisibleTo` for the integration test project and the DI package (AC-3 met).
- `BybitHttpClient` is `sealed`, primary constructor takes only `HttpClient` (recv-window owned by the signing handler — justified deviation).
- `JsonOptions` block identical to Binance.
- Build: 0 warnings / 0 errors. Tests: 135 pass.

---

## Next action for orchestrator

CHANGES_REQUESTED. Route B1 back to the implementer (single trivial fix: add the `endpoint` guard to all three methods). Re-run the gate after the fix. N1–N4 are optional and need not block re-approval.
