# Review Gate Aggregate — TASK-003: BybitSigningHandler

**Commit**: 283bcf0
**Date**: 2026-06-17
**Branch**: feat/m2-exchange-expansion
**Gate policy**: require_all_approve=true, confidence_threshold=80, auto_approve_concerns=true, block_on_security_reject=true

## Pre-checks
- Build (`dotnet build CryptoExchanges.Net.sln`): **0 Warning(s), 0 Error(s)** (re-confirmed)
- Tests (`dotnet test --filter 'Category!=Integration'`): all pass (135 green, per orchestrator)

## Reviewer Verdicts

| Reviewer | Verdict | Confidence |
|---|---|---|
| architect-reviewer | APPROVE | 97 |
| code-reviewer | APPROVE | (approved) |
| security-reviewer | APPROVE | (no blocking; 1 CONCERN @ 82) |
| api-reviewer | CHANGES_REQUESTED | 2 REJECT (95, 80) |

## Aggregate Verdict: CHANGES_REQUESTED

Three of four reviewers APPROVED. The api-reviewer raised two REJECT findings at/above the
confidence threshold (80), so by mechanical gate policy the aggregate is CHANGES_REQUESTED.
No security REJECT (security-reviewer APPROVED), so `block_on_security_reject` is not triggered.

## Blocking Items (api-reviewer)

### REJECT-1 (conf 95): `BybitSigningHandler` and `BybitSignatureService` should be `internal`, not `public`
- Files: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13`,
  `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13`
- Claim: csproj `InternalsVisibleTo` already grants the only two legitimate consumers
  (`...Bybit.Tests.Integration`, `...DependencyInjection`); the csproj comment lists "signing" as
  an internal type. Public ctors lock infrastructure internals as committed API.
- Fix proposed: change both to `internal sealed class`.

### REJECT-2 (conf 80, CONDITIONAL): `recvWindow` ctor param as `string` is a weak public contract
- File: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13-14`
- Explicitly conditioned on REJECT-1: "if type becomes `internal` this downgrades to
  CONCERN/non-blocking." Blocking "only if public modifier retained."
- Fix proposed: if public, use `int`/`long` and format internally, and reorder so `timeOffset`
  is last (Binance parity). If internal, the string form is acceptable.

## Gate Note — material contradiction the orchestrator/human should weigh before forcing a fix

Both blocking findings argue that TASK-003 should DIVERGE from the established, committed sibling
pattern, not that the task contains a defect against the pattern it was told to follow:

- The verified Binance siblings are ALL `public` today, with byte-identical csproj wording:
  - `BinanceSigningHandler` — `public sealed class` (Resilience/BinanceSigningHandler.cs:12)
  - `BinanceSignatureService` — `public sealed class` (Auth/BinanceSignatureService.cs:9)
  - `BinanceSigningRequest` — `public static class` (Resilience/BinanceSigningRequest.cs:5)
  - Binance csproj `InternalsVisibleTo` comment is identical ("wire internal types ... signing").
- The objective mandates each exchange "clones the verified Binance pattern"; Nazgul Rule 3
  mandates "Follow existing patterns exactly." TASK-003 matched the public sibling shape exactly.
- The api-reviewer itself concedes Binance "is also public today ... a pre-existing issue tracked
  separately; this PR should not worsen it." REJECT-2 is admittedly non-blocking once the type is
  internal, i.e. it is parasitic on REJECT-1.

Disposition options for the orchestrator:
1. **Accept the findings as a convention change** — make the Bybit signing types `internal`. This
   creates Binance/Bybit asymmetry across exchange modules unless Binance is changed in the same
   sweep (out of TASK-003 scope).
2. **Treat both as non-defect CONCERNs / follow-up** — keep TASK-003 consistent with the verified
   public sibling pattern, and track "make all exchange signing types internal" as a separate,
   project-wide API task (the api-reviewer's own preferred framing for the Binance equivalents).

Recommendation: option 2 is the more defensible call for THIS task — it preserves the
clone-Binance mandate and does not introduce cross-module asymmetry. The internal-visibility
decision is a legitimate, valuable API improvement but belongs in a dedicated symmetric task
covering all exchanges, not as a blocker that singles out the new module for diverging from its
own reference pattern.

## Non-blocking CONCERNs (auto-approved per auto_approve_concerns=true)
- security-reviewer (conf 82): empty `apiKey` on a signed request throws in `ResignAsync` rather
  than at construction; no security impact. Optional: add
  `ArgumentException.ThrowIfNullOrWhiteSpace(apiKey)` to the ctor.
- architect-reviewer (conf 72): DELETE falls through to the GET signing branch implicitly; correct
  today but the `else` branch could name the GET/DELETE grouping in a comment.
- api-reviewer (conf 90): missing `<param>` XML docs on the four ctor params.
- api-reviewer (conf 70): `BybitSigningRequest` public visibility misleading but mirrors Binance.
- api-reviewer (conf 85): guard asymmetry in `BuildGetSignString`/`BuildPostSignString`; moot at
  internal visibility.
