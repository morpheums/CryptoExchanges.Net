---
id: TASK-024
status: IMPLEMENTED
commit: 7eb04af
claimed_at: 2026-06-19
---

# TASK-024: Code-hygiene cleanup — file-per-type split + comment hygiene

**Status**: IMPLEMENTED

**Commits**: Phase 1 `18fe22f` (file-per-type split), Phase 2 `7eb04af` (comment hygiene).
Build 0W/0E (Release); 455 tests pass; sweep clean; no banners; no API change.
Executed directly from the approved spec/plan on branch `chore/cleanup-file-per-type`; PR opened for human review/merge.

**Blast radius**: LOW — pure refactor across the whole solution. No namespace, visibility,
modifier, or behavior changes. The existing unit + integration tests (455 total) are the
regression net and must stay green; build must remain 0W/0E.

## Scope
Standalone pre-feature cleanup on branch `chore/cleanup-file-per-type`, driven by the approved
spec `docs/superpowers/specs/2026-06-18-code-cleanup-file-per-type-and-comments-design.md` and
plan `docs/superpowers/plans/2026-06-18-code-cleanup-file-per-type-and-comments.md`.

Two phases, two commits:
1. **Phase 1 (file-per-type):** every top-level type in its own file named after the type;
   per-exchange wire DTOs extracted from `Services/*.cs` and `*ExchangeClient.cs` into per-exchange
   `Dtos/` folders (namespaces unchanged). Core `Models.cs`/`Enums.cs`/`ExchangeExceptions.cs`/
   `IExchangeClient.cs` split; `HmacSignature`, `AssetJsonConverter`, `TransientExhaustionHandler`,
   `CryptoExchangesOptions`, `BinanceOptions` extracted. (COMMITTED — 18fe22f)
2. **Phase 2 (comment hygiene):** delete banner separators and code-restating comments; keep/tighten
   genuine why/quirk rationale; ensure interfaces carry full XML docs and implementations use
   `<inheritdoc/>`.

## Acceptance
- Build 0W/0E (Release); all 455 tests pass.
- Solution-wide one-type-per-file sweep clean.
- No banner separators remain in `src/**/*.cs`.
- No public API surface change.
