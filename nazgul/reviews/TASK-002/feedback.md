# TASK-002 Review Gate — Aggregate Feedback

**Task**: BybitSignatureService + signing request marker
**Commit**: 5654d93
**Branch**: feat/m2-exchange-expansion
**Aggregate Verdict**: **CHANGES_REQUESTED**

## Gate Policy Applied
- `require_all_approve: true` — all four reviewers must approve
- `confidence_threshold: 80` — CONCERNs below 80 are non-blocking (auto-approved)
- `auto_approve_concerns: true`
- `block_on_security_reject: true` — N/A here (security APPROVED)

## Reviewer Verdicts
| Reviewer | Verdict | Top Confidence | Blocking? |
|----------|---------|----------------|-----------|
| architect-reviewer | APPROVE | concerns max 70 | No |
| code-reviewer | CHANGES_REQUESTED | REJECT @ 85 | **Yes** |
| security-reviewer | APPROVE | concerns max 65 | No |
| api-reviewer | APPROVE | concerns max 78 | No |

## Blocking Items (MUST fix — at/above confidence 80)

### [REJECT, confidence 85, code-reviewer] Missing input guards on static sign-string builders
**File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:37-53`

`BuildGetSignString(string timestamp, string apiKey, string recvWindow, string queryString)` and `BuildPostSignString(string timestamp, string apiKey, string recvWindow, string jsonBody)` accept eight public string parameters with zero input validation. The project convention `ArgumentException.ThrowIfNullOrWhiteSpace` is mandatory at public boundaries. Null or whitespace inputs silently produce a malformed sign-string instead of failing at the boundary, which surfaces later as an opaque Bybit auth error.

**Required fix**: Add `ArgumentException.ThrowIfNullOrWhiteSpace` (or `ArgumentNullException.ThrowIfNull` where empty is legitimately allowed — note `queryString` may be empty for parameterless GETs, and `jsonBody` for empty-body POSTs) for the identity/protocol fields `timestamp`, `apiKey`, `recvWindow`. Decide per-parameter whether whitespace-empty is valid; `queryString`/`jsonBody` likely use `ThrowIfNull` only.

## Non-Blocking Concerns (below threshold — auto-approved, recorded for awareness)
- **code-reviewer @ 70**: `Sign(string)` lacks a null guard before `Encoding.UTF8.GetBytes` (matches Binance precedent).
- **architect @ 55**: Both types are `public` rather than `internal` + `InternalsVisibleTo` — but mirrors shipped Binance precedent exactly.
- **architect @ 60 / code @ 60**: No `ISignatureService` interface yet — forward-compat note for the OKX generalization phase.
- **api-reviewer @ 72**: `BuildGetSignString`/`BuildPostSignString` exposed as public API leak an intermediate signing detail.
- **api-reviewer @ 78**: `timestamp`/`recvWindow` typed as `string` rather than `long` — type-safety gap; worth fixing before the signing handler lands to avoid a later breaking change.
- **api-reviewer @ 68**: Public static builders may require a breaking change during OKX abstraction generalization.
- **security @ 65**: `Sign` lacks explicit null guard (consistent with Binance, controlled call path).
- **security @ 50**: `BybitOptions.SecretKey` has no `ToString()` redaction / `[JsonIgnore]` — out of TASK-002 scope (pre-existing from TASK-001).

## Required Action
Address the single blocking finding (input guards on the static sign-string builders), then re-run the gate. The three approving reviewers raised no blockers. Note: the guards being on the builders interacts with the `string` vs `long` typing concern (api-reviewer @ 78) — if the implementer chooses to retype `timestamp`/`recvWindow` to `long` while fixing guards, that resolves both, but it is not required to pass the gate.
