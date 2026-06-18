# Review Gate Feedback — TASK-009: OKX-era credential/signing generalization (Core)

**Commit**: 63b0006
**Branch**: feat/m3-okx
**Date**: 2026-06-18
**Gate policy**: confidence_threshold=80, require_all_approve=true, block_on_security_reject=true, auto_approve_concerns=true

## Aggregate Verdict: APPROVED

All four reviewers approved. No REJECT findings at or above the confidence threshold. No security REJECT. All CONCERNs are below threshold or non-blocking and are auto-approved.

| Reviewer | Verdict | Confidence |
|---|---|---|
| architect-reviewer | APPROVED | 97 |
| security-reviewer | APPROVED | 99 |
| api-reviewer | APPROVED | 96 |
| code-reviewer | APPROVED | (PASS; findings 99–100, two CONCERNs at 85/70) |

## Blocking Items: NONE

## Non-blocking CONCERNs (auto-approved; optional follow-up polish)

1. **XML param doc wording** (api-reviewer, conf 90) — `SignatureEncoding.cs:32-33` and `ExchangeCredentials.cs:30-31`: `<param>` summaries say "Must be non-empty." but the guard is `ThrowIfNullOrWhiteSpace` (also rejects whitespace). The `<exception>` docs already say "null/empty/whitespace". Suggested: change "Must be non-empty." to "Must be non-null, non-empty, and non-whitespace." on `secret`, `payload`, `apiKey`, `secretKey`. Four one-line edits.

2. **Missing exactly-4-char ApiKey boundary test** (code-reviewer, conf 85) — `AuthTests.cs:143-146`: masking tests cover 2-char and 16-char keys but not the `Length == 4` boundary where `<= 4` takes the full-mask branch. Behavior is correct; boundary untested. Suggested: add a `"abcd"` case asserting `ApiKey = ****` and not containing `abcd`.

3. **Record Equals/GetHashCode compares secrets by value** (api-reviewer, conf 65) — `ExchangeCredentials.cs:13`: synthesized record equality compares SecretKey/Passphrase via plain string comparison. Not an exploitable side channel for a configuration object in this SDK's threat model. No fix required; `CryptographicOperations.FixedTimeEquals` is the upgrade path if ever raised.

4. **Record PrintMembers still synthesized** (code-reviewer, conf 70 / security-reviewer, conf 99) — `ExchangeCredentials.cs:13,59`: PrintMembers is synthesized but unreachable because the type is `sealed` (no derived caller) and the overridden `ToString()` never delegates to it. Security-reviewer confirmed at 99 confidence that no path can render SecretKey/Passphrase. No action.

## Key verifications confirmed
- **Additive/non-breaking**: commit 63b0006 touches only `Core/Auth/ExchangeCredentials.cs` (new), `Core/Auth/SignatureEncoding.cs` (new), `AuthTests.cs` (new), and the manifest. Zero Binance/Bybit/Http/DI source edits (architect + api + code reviewers, conf 100).
- **HMAC correctness**: BCL `HMACSHA256.HashData(key=secret, message=payload)`, UTF-8, no homemade crypto; hex output byte-identical to Binance pinned vector; base64 independently re-derived (security + code reviewers, conf 100).
- **Secret redaction**: sealed-record `ToString()` override fully suppresses synthesized PrintMembers; SecretKey/Passphrase never rendered (security-reviewer, conf 99).
- **Http seam**: `Func<IServiceProvider, DelegatingHandler>?` is exchange-agnostic and can host an OKX base64+passphrase+header signer with no Http change (architect-reviewer confirmed).
- **Build/tests**: 0 warnings / 0 errors under TreatWarningsAsErrors; 239 tests pass incl. +24 new Core, no Binance/Bybit regression.

## Recommended next state: IMPLEMENTED → DONE (review gate passed)
