---
id: TASK-027
status: DONE
commit: 8821088
claimed_at: 2026-06-19
---

# TASK-027: Document code conventions (file-per-type, DTO naming house rule) in CLAUDE.md

**Status**: DONE (committed 8821088 on main)

**Blast radius**: NONE — documentation only (CLAUDE.md). No code, no tests.

## Scope
Capture the architect-ruled conventions established across TASK-024/025/026 so future
work (e.g. the 5th exchange) follows them automatically: one-type-per-file, internal
per-exchange `Dtos/`, the `{Concept}Dto` canonical naming house rule (reserved
`ResponseDto<T>`/`ResponseObjectDto<T>`/`ListDto<T>` wrappers; vendor terms only in
`[JsonPropertyName]`; balance leaf→`BalanceDto`/container→`AccountDto`; fills→`FillDto`),
and the comment policy. Source of truth: `nazgul/reviews/DECISION-DTO-NAMING/architect-reviewer.md`.

## Acceptance
- CLAUDE.md gains a concise "Code Conventions" section; no other files changed.
