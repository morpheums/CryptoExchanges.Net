---
status: DONE
---

# PATCH-001 — Post-loop documentation for FEAT-008

**Type**: documentation
**Created**: 2026-06-24

## Description

Post-loop documentation pass for FEAT-008 (stream control-message rate limit / multi-symbol
streaming reconnect-loop fix). Adds CHANGELOG.md entry covering:

1. WebSocket multi-symbol streaming reconnect-loop fix (Binance + KuCoin)
2. Binance combined-stream order-book decoder fix (data-envelope unwrap)

## Files

- `CHANGELOG.md` — added `### Fixed` entries under `## [Unreleased]`

## Commits

feat(FEAT-008): post-loop CHANGELOG entry for streaming reconnect-loop + order-book decode fix
