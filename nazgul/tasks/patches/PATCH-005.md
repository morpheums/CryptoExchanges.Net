# PATCH-005: Release prep for v0.5.0-preview.3

## Metadata
- **Status**: IN_PROGRESS
- **Created**: 2026-06-24T00:00:00Z
- **Source**: /nazgul:patch
- **Flags**: none

## Description
Release prep for v0.5.0-preview.3 (next preview after the merged reconnect-race fix, PR #43 / PATCH-003 + PATCH-004 now on main). Two file edits only, no code changes:

1. Directory.Build.props — bump <Version> from 0.5.0-preview.2 to 0.5.0-preview.3.
2. CHANGELOG.md — add a new dated section directly under the "## [Unreleased]" marker, ABOVE the existing "## [0.5.0-preview.2]" section, in Keep a Changelog style documenting the Binance ObjectDisposedException reconnect-race fix.

Keep the "## [Unreleased]" header in place (empty) above the new section. Release bookkeeping only — branch + PR to main (protected), squash-merge ready. Do NOT create any git tag.

## Subtasks
1. Bump <Version> 0.5.0-preview.2 → 0.5.0-preview.3 in Directory.Build.props
2. Insert new [0.5.0-preview.3] — 2026-06-24 CHANGELOG section under [Unreleased], above [0.5.0-preview.2]

## Implementation Log
- Subtask 1: Bumped `<Version>` 0.5.0-preview.2 → 0.5.0-preview.3 in Directory.Build.props.
- Subtask 2: Inserted `## [0.5.0-preview.3] — 2026-06-24` Fixed section into CHANGELOG.md under [Unreleased], above [0.5.0-preview.2]. [Unreleased] header left in place (empty).
