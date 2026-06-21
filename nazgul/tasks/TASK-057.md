---
id: TASK-057
status: IMPLEMENTED
depends_on: [TASK-056]
---
# TASK-057: KC-API passphrase-v2 signing service + mark-and-strip signing handler

## Metadata
- **ID**: TASK-057
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-056
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs, src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs, src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningRequest.cs, src/CryptoExchanges.Net.Kucoin/Resilience/KucoinErrorTranslator.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinSigningTests.cs]
- **Wave**: 2
- **Traces to**: PRD-FEAT-006 AC-2; TRD-FEAT-006 §"Signing — KC-API Passphrase-v2"; FEAT-006 spec §"Signing", §"Build approach" step 2; TEST-PLAN-FEAT-006 §1, §2
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**: 2026-06-21T00:00:00Z
- **Base SHA**: 9da0981
- **Implemented at**: 2026-06-21T08:00:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 1/3
- **Test failures**: 0

## Description

Implement KuCoin's `KC-API` passphrase-v2 signing, TDD, cloning the OKX signing pattern and adjusting
the two KuCoin-specific differences: the passphrase is itself HMAC-SHA256-signed + base64-encoded, and
the timestamp is Unix epoch **milliseconds** (string), not ISO-8601. Reuse the existing
`ExchangeCredentials` (passphrase already supported) and `Core.Auth` primitives.

Create:
- **`Auth/KucoinSignatureService.cs`** — clone `OkxSignatureService`. Exposes:
  `Sign(prehash)` → `Convert.ToBase64String(HMAC-SHA256(secret, prehash))`;
  `SignPassphrase(passphrase)` → `Convert.ToBase64String(HMAC-SHA256(secret, passphrase))`;
  `static string FormatTimestamp(DateTimeOffset)` → Unix epoch ms as string;
  `static string BuildPrehash(timestamp, method, requestPath, body)` → `timestamp + METHOD + requestPath + body`.
  Full XML docs on the interface/methods.
- **`Resilience/KucoinSigningRequest.cs`** — clone `OkxSigningRequest`: the per-request marker that
  prevents double-signing on retry (mark-and-strip).
- **`Resilience/KucoinSigningHandler.cs`** — clone `OkxSigningHandler` (`DelegatingHandler`,
  mark-and-strip, **per-attempt re-sign** so a GET retry re-signs with a fresh timestamp). Strips and
  re-adds on every attempt: `KC-API-KEY`, `KC-API-SIGN`, `KC-API-TIMESTAMP`, `KC-API-PASSPHRASE`,
  `KC-API-KEY-VERSION: 2`. Unsigned (unmarked) requests pass through untouched. Throw
  `InvalidOperationException` when api-key or passphrase is empty. Retry stays GET-only (same Polly
  config as OKX — this handler does not change the resilience pipeline; it only signs).
- **`Resilience/KucoinErrorTranslator.cs`** — clone `OkxErrorTranslator`: maps KuCoin's
  `{"code","msg"}` envelope (success code `"200000"`) → typed `Core` exceptions.

Tests (`KucoinSigningTests.cs`) — golden-value + behavior, no network:
- `Sign` base64 HMAC-SHA256 golden value; `SignPassphrase` base64 HMAC-SHA256 of passphrase golden
  value; `BuildPrehash` exact concatenation order; `FormatTimestamp` returns Unix ms string (NOT
  ISO-8601).
- Handler: unsigned request passes through (no KC-API-* headers); marked request has all five headers
  incl. `KC-API-KEY-VERSION: 2`; calling the handler twice (simulated retry) yields differing
  timestamps with no duplicate headers; missing api-key throws; missing passphrase throws.

## Acceptance Criteria
- [ ] `KucoinSignatureService` (Sign + SignPassphrase both base64 HMAC-SHA256; `FormatTimestamp` = Unix epoch **ms** string; `BuildPrehash` = `timestamp+METHOD+requestPath+body`) and `KucoinSigningHandler` (mark-and-strip, per-attempt re-sign, five headers incl. `KC-API-KEY-VERSION: 2`, unsigned pass-through, retry-GET-only) exist with full XML docs, one type per file.
- [ ] `KucoinSigningTests` cover golden-value Sign/SignPassphrase, prehash order, Unix-ms timestamp, all-five-headers, retry-resign-fresh-timestamp, missing-key/passphrase throws — all with NO network; solution builds 0W/0E.
- [ ] `KucoinErrorTranslator` maps `{"code","msg"}` (success `"200000"`) → typed `Core` exceptions, mirroring `OkxErrorTranslator`; existing non-integration suite stays green.

## Pattern Reference
- Signing service to clone: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs` (base64 HMAC-SHA256, prehash builder).
- Signing handler + marker: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs` + `OkxSigningRequest.cs` (mark-and-strip, per-attempt re-sign, passphrase header).
- Error translator: `src/CryptoExchanges.Net.Okx/Resilience/OkxErrorTranslator.cs`.
- Signing tests shape: `tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxSigningTests.cs`.
- Credentials primitive: `src/CryptoExchanges.Net.Core/Auth/` (`ExchangeCredentials` — Passphrase supported).

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs
- src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs
- src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningRequest.cs
- src/CryptoExchanges.Net.Kucoin/Resilience/KucoinErrorTranslator.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinSigningTests.cs

**Modifies**:
- (none — additive)

## Traceability
- **PRD Acceptance Criteria**: AC-2 (passphrase-v2 signing, per-attempt re-sign, retry GET-only), AC-7 (fake/no-network unit tests)
- **TRD Component**: §"Signing — KC-API Passphrase-v2" (OKX-vs-KuCoin diff table)
- **ADR Reference**: FEAT-006 spec §"Binding constraints" (retry-only-on-GET, per-attempt re-sign)

## Commits

- a754e9f feat(FEAT-006): TASK-057 IMPLEMENTED (KC-API passphrase-v2 signing + handler)
- d3bf817 feat(FEAT-006): TASK-057 IMPLEMENTED (DIP fix — IKucoinSignatureService)

## Implementation Log

### 2026-06-21 — Retry 1: DIP fix (IKucoinSignatureService)

- Blocking finding resolved: introduced `IKucoinSignatureService : ISignatureService` in `Auth/`
  with `string SignPassphrase(string passphrase)` (full XML docs).
- `KucoinSignatureService` now implements `IKucoinSignatureService` (instead of `ISignatureService`
  directly); all member implementations use `<inheritdoc/>` unchanged.
- `KucoinSigningHandler` primary constructor parameter changed from concrete `KucoinSignatureService`
  to `IKucoinSignatureService` — DIP satisfied, handler is now testable with any mock that
  implements the interface.
- Test helper `BuildHandler` compiles unchanged: `new KucoinSignatureService(secret)` satisfies
  `IKucoinSignatureService` via the new inheritance chain.
- Build: 0W/0E. KuCoin unit tests: 44/44 passed.

### 2026-06-21 — Initial implementation

- Cloned OKX signing pattern; adjusted for KuCoin passphrase-v2 differences:
  - `KucoinSignatureService`: added `SignPassphrase()` (HMAC-SHA256 + base64 of the passphrase itself);
    `FormatTimestamp()` returns Unix epoch milliseconds string (not ISO-8601).
  - `KucoinSigningHandler`: strips/re-adds 5 headers on each attempt including `KC-API-KEY-VERSION: 2`;
    passphrase passed through `SignPassphrase()` before being placed on the wire.
  - `KucoinSigningRequest`: identical mark-and-strip marker pattern to OKX.
  - `KucoinErrorTranslator`: maps `{"code","msg"}` (success `"200000"`) to typed exceptions.
- `KucoinSigningTests`: 44 tests total (38 new) — all passing; golden values independently verified with Python.
- Build: 0W/0E across full solution.

## Review Results

- architect-reviewer: CHANGES_REQUESTED — KucoinSigningHandler takes concrete KucoinSignatureService instead of ISignatureService (DIP violation, Architectural Rule #11)
- code-reviewer: APPROVE — all conventions pass (LR-001, LR-005 satisfied); minor non-blocking style notes
- security-reviewer: APPROVE — all 11 security checks pass; prehash order, base64 HMAC, passphrase-v2 signing, per-attempt re-sign, mark-and-strip, Unix-ms timestamp all confirmed correct
- api-reviewer: CHANGES_REQUESTED — same blocking finding as architect: concrete type in handler constructor

## Consolidated Feedback

**Blocking fix required** (1 item):

Introduce `internal interface IKucoinSignatureService : ISignatureService` in `Auth/` with `string SignPassphrase(string passphrase)`. Three file changes:
1. CREATE `src/CryptoExchanges.Net.Kucoin/Auth/IKucoinSignatureService.cs`
2. EDIT `src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs:12` — implement `IKucoinSignatureService` instead of `ISignatureService`
3. EDIT `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:21` — change parameter type from `KucoinSignatureService` to `IKucoinSignatureService`; update test `BuildHandler` helper type accordingly

Full feedback: `nazgul/reviews/TASK-057/consolidated-feedback.md`
