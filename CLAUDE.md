# Nazgul Framework — Project Instructions

## Architecture
This project uses the Nazgul autonomous development pipeline:

```
Objective → Discovery (+ Classification) → Doc Generator → Planner → Implementer → Review Board → Loop → Post-Loop → Complete
```

## Key Files
- `nazgul/config.json` — Runtime configuration (mode, iteration, reviewers)
- `nazgul/plan.md` — Live task tracker with Recovery Pointer
- `nazgul/tasks/` — Individual task manifests with full state
- `nazgul/checkpoints/` — Per-iteration JSON snapshots
- `nazgul/reviews/` — Review artifacts per task
- `nazgul/context/` — Project context from Discovery
- `nazgul/docs/` — Generated project documents (PRD, TRD, ADRs)

## Commands
- `/nazgul-init` — First-time setup: run Discovery, generate reviewers, create runtime dirs
- `/nazgul-start` — Auto-detects project state and continues or starts work (derives objective from context)
- `/nazgul-start "objective"` — Override: start a specific new objective (flags: --afk, --yolo, --hitl, --max N)
- `/nazgul-status` — Check loop progress, task counts, reviewer board
- `/nazgul-task` — Task lifecycle: skip, unblock, add, prioritize, info, list
- `/nazgul-pause` — Gracefully pause the loop at next iteration boundary
- `/nazgul-log` — View run history: iterations, commits, reviews, blockers
- `/nazgul-reset` — Archive current state and reset to clean slate
- `/nazgul-clean` — Fully remove Nazgul from this project
- `/nazgul-review` — Manually trigger review for a task
- `/nazgul-discover` — Re-run codebase discovery
- `/nazgul-context` — Collect targeted context for an objective type
- `/nazgul-simplify` — Post-loop cleanup pass on modified files
- `/nazgul-docs` — View or regenerate project documents
- `/nazgul-patch` — Lightweight task mode for bug fixes, config changes, and small features
- `/nazgul-verify` — Human acceptance testing for completed tasks
- `/nazgul-help` — Quick reference for all commands and modes

## The 10 Rules for the Nazgul Loop

1. **Always read plan.md first.** The Recovery Pointer tells you exactly where you are.
2. **Files are truth, context is ephemeral.** Write state to files immediately. Never rely on conversational memory.
3. **Follow existing patterns exactly.** Read the pattern reference before implementing. Match the style.
4. **Tests are mandatory.** Every task includes tests. Run them after every change. Don't proceed if failing.
5. **Never skip the review gate.** ALL reviewers must approve. No exceptions.
6. **Address ALL blocking feedback.** When CHANGES_REQUESTED, fix every REJECT item.
7. **One task at a time.** Don't work on multiple tasks simultaneously (unless parallel mode with Agent Teams).
8. **Update Recovery Pointer on every state change.** This is how you survive compaction. Evidence gates enforce real work: IMPLEMENTED requires a commit SHA in the manifest, IN_REVIEW requires a review directory, source edits require an IN_PROGRESS task.
9. **Commit in AFK mode.** Every state transition gets a commit with the dynamic prefix from config (e.g., `feat(FEAT-003):`).
10. **NAZGUL_COMPLETE means ALL tasks DONE and post-loop finished.** Not before.

## Git Convention
- The default branch is `main`. All Nazgul branch operations (stacking, PRs, merges) target `main`.

## Code Conventions
- **One type per file.** Every top-level type (record/class/DTO/enum/interface/struct/exception) lives in its own file named after the type. No exchange prefix on file or type names — the namespace carries the exchange.
- **Wire DTOs** are `internal` and live in each exchange's `Dtos/` folder. `Core` is the shared cross-exchange contract (`Core.Models`/`Core.Interfaces`); never add a shared DTO project.
- **DTO naming (house rule).** Name every wire DTO `{Concept}Dto` using the *canonical* `Core.Models` concept it maps to — identical across all exchanges regardless of the vendor's term (`TickerDto`, `OrderDto`, `TradeDto`, `OrderBookDto`, `ServerTimeDto`, `FillDto`, `SymbolInfoDto`, `BalanceDto`, `AccountDto`). Vendor vocabulary lives ONLY in `[JsonPropertyName]`, never in the type name.
  - No `Response`/`Result`/`History` on leaf DTOs. Reserved wrappers only: `ResponseDto<T>` / `ResponseObjectDto<T>` (transport envelope — the ONLY use of "Response") and `ListDto<T>` (a `{ list:[...] }` payload — the ONLY use of "List"). Never write a typed list-wrapper.
  - Two-level balance shapes: per-asset leaf → `BalanceDto`, container → `AccountDto`. An exchange's own executed-trades/fills shape → `FillDto`.
- **Comments.** No banner separators or comments that restate the code. Keep comments only for non-obvious business logic / exchange quirks. Interfaces carry full `<summary>`/`<param>`/`<exception>` XML docs; implementations use `<inheritdoc/>`.

## Safety
- Pre-tool guard blocks destructive commands (rm -rf /, DROP TABLE, etc.)
- Security rejections in AFK mode → BLOCKED (requires human review)
- Max retries per task: 3 (configurable in config.json)
- Max consecutive failures: 5 (auto-stops if no progress)

## Recovery
After any interruption (compaction, crash, timeout):
1. Read `nazgul/plan.md` → Recovery Pointer
2. Read latest checkpoint in `nazgul/checkpoints/`
3. Read active task manifest in `nazgul/tasks/`
4. Resume from the Next Action
