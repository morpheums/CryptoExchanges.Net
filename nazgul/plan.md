# Nazgul Plan

## Objective
<!-- Set by the Planner agent after reading context files and user objective.
     Example: "Add Stripe payment integration with checkout flow, webhook handling, and subscription management."
     This must be a clear, specific statement of what the loop will accomplish. -->

## Discovery Status
<!-- Updated by the Discovery agent after scanning the codebase. -->
- [ ] Discovery run: <!-- timestamp, e.g. 2026-02-27T14:30:00Z -->
- [ ] Classification: <!-- greenfield | brownfield | refactor | bugfix | migration -->
- [ ] Reviewers generated: <!-- comma-separated list, e.g. architect-reviewer, code-reviewer, security-reviewer -->
- [ ] Context collected: <!-- scope type, e.g. feature-scope, refactor-scope, bugfix-scope -->
- [ ] Documents generated: <!-- comma-separated list, e.g. TRD, ADR-001, test-plan -->

## Status Summary
<!-- Auto-updated by agents after each state transition.
     The stop hook and session-context hook read this section for quick state assessment.
     Every agent that changes task state MUST update these counters. -->
- Total tasks: 0
- DONE: 0 | READY: 0 | IN_PROGRESS: 0 | IN_REVIEW: 0 | CHANGES_REQUESTED: 0 | BLOCKED: 0 | PLANNED: 0
- Current iteration: 0/40
- Active task: none

## Parallel Groups
<!-- Tasks within a group can run simultaneously (zero file overlap, zero dependencies).
     Tasks across groups MUST run sequentially — each group completes before the next starts.
     The Planner populates these groups during planning.
     The stop hook checks group completion to advance to the next group. -->

### Group 1
<!-- Example:
- [ ] TASK-001: Create user model (files: src/models/user.ts, tests/models/user.test.ts) -> PLANNED
- [ ] TASK-002: Create product model (files: src/models/product.ts, tests/models/product.test.ts) -> PLANNED
-->

## Tasks
<!-- Individual task entries are maintained in nazgul/tasks/TASK-NNN.md manifests.
     This section serves as a quick-reference index.
     Status key: PLANNED | READY | IN_PROGRESS | IMPLEMENTED | IN_REVIEW | CHANGES_REQUESTED | DONE | BLOCKED

     Example entry:
     ### TASK-001: Create user model
     - **Status**: DONE
     - **Group**: 1
     - **Depends on**: none
     - **Manifest**: nazgul/tasks/TASK-001.md
-->

## Completed
<!-- Tasks moved here after ALL reviewers approve and status is set to DONE.
     Each entry includes the completion commit SHA for traceability.

     Example:
     - [x] TASK-001: Create user model -> DONE (sha: ghi9012)
     - [x] TASK-002: Create product model -> DONE (sha: jkl3456)
-->

## Blocked
<!-- Tasks that hit max retries, have unresolvable issues, or require human intervention.
     Each entry includes the reason and whether human input is required.

     Example:
     - TASK-008: Add payment integration -> BLOCKED (requires human: API keys needed)
       - Reason: Stripe API keys not configured in environment
       - Attempted: 2 retries, both failed on missing STRIPE_SECRET_KEY
       - Action needed: User must add API keys to .env and set status to READY
-->

## Recovery Pointer
<!-- THE MOST IMPORTANT SECTION IN THIS FILE.
     Updated on EVERY state transition by the acting agent.
     This is the FIRST thing read by:
       - The stop hook re-injection message
       - session-context.sh on startup/compaction
       - Any agent starting work after interruption
     It must be small enough to survive aggressive compaction summaries.
     It must be human-readable for manual inspection. -->

- **Current Task:** none
- **Last Action:** Plan created, no tasks started
- **Next Action:** Run discovery, then begin task execution
- **Last Checkpoint:** none
- **Last Commit:** none
