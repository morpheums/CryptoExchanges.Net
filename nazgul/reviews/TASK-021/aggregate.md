# Review Board Aggregate — TASK-021: BitgetHttpClient + IBitgetHttpClient

**Date**: 2026-06-18
**Branch**: feat/m4-bitget · **Commit**: e11cdfb
**Policy**: require_all_approve=true · confidence_threshold=80 · block_on_security_reject=true
**Pre-checks**: build 0W/0E (orchestrator-verified); no tests this task (arrive in TASK-022)
**Scope**: src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs, src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs (TASK-019 BitgetSigningHandler excluded)

## Aggregate Verdict: APPROVED

All four reviewers APPROVED. No blocking findings (none with confidence >= 80 AND severity HIGH/MEDIUM). No security REJECT.

| Reviewer | Verdict | Confidence | Blocking items |
|----------|---------|-----------|----------------|
| architect-reviewer | APPROVED | 97 | none |
| code-reviewer | APPROVED | 100 | none |
| security-reviewer | APPROVED | high | none |
| api-reviewer | APPROVED | 100 | none |

## Sign-consistency invariant — UPHELD
BaseAddress host-only → client builds full `/api/v2/...` path + escaped query → `RequestUri.AbsolutePath` = signed requestPath, `RequestUri.Query` = signed query, byte-consistent with `BitgetSigningHandler`'s separate path/query `BuildPrehash`. Client only `MarkSigned`s; no inline signing; no secret handling. Confirmed by architect (97) and security review.

## Non-blocking CONCERNs (auto-approved, confidence < 80 or LOW severity)
1. **BaseAddress host-root not code-enforced** (architect conf 65, security conf 40, LOW) — cross-task dependency. When the DI/composer task lands, validate `options.BaseUrl` AbsolutePath is `/` (or guard in ctor) so the invariant is self-enforcing rather than documentation-only. Out of scope for these two files.
2. **`ReadFromJsonAsync<T>!` null-forgiving / no JsonException guard** (code conf 55, security conf 55, LOW) — shared codebase pattern (OKX/Binance identical); accepted given pipeline guarantees 2xx-only typed pass-through. Revisit if empty-body/non-JSON 2xx becomes possible.
3. **Class XML doc length** (architect conf 80, LOW severity — non-blocking quality preference) — second paragraph (sign-consistency invariant) is load-bearing; paragraphs 1 and 3 could be trimmed in a follow-up consistency pass (OKX doc is equally verbose).

## Notes
- code-reviewer independently ran full suite: build 0W/0E under TreatWarningsAsErrors, 226 unit tests pass.
- Implementation is a faithful clone of the verified OKX wrapper with the correct adaptation (OKX signs single `PathAndQuery`; Bitget handler reads `AbsolutePath`+`Query` separately).
