---
status: DONE
---

# PATCH-002 — Post-loop release management for FEAT-008 (v0.5.0-preview.2)

**Type**: release
**Created**: 2026-06-24

## Description

Post-loop release management pass for FEAT-008. Bumps the version from `0.5.0-preview.1` to
`0.5.0-preview.2` (preview-counter increment for a bug-fix release with no public API change),
promotes the CHANGELOG `[Unreleased]` entries to the new version heading, and validates packaging.

## Files

- `Directory.Build.props` — version bump `0.5.0-preview.1` → `0.5.0-preview.2`
- `CHANGELOG.md` — promote `[Unreleased]` entries to `[0.5.0-preview.2] — 2026-06-24`

## Pack validation

9/9 packages built successfully at `0.5.0-preview.2`. Zero errors, zero warnings.

Packages produced:
- `CryptoExchanges.Net.Core.0.5.0-preview.2.nupkg`
- `CryptoExchanges.Net.Http.0.5.0-preview.2.nupkg`
- `CryptoExchanges.Net.Binance.0.5.0-preview.2.nupkg`
- `CryptoExchanges.Net.Bybit.0.5.0-preview.2.nupkg`
- `CryptoExchanges.Net.Okx.0.5.0-preview.2.nupkg`
- `CryptoExchanges.Net.Bitget.0.5.0-preview.2.nupkg`
- `CryptoExchanges.Net.Kucoin.0.5.0-preview.2.nupkg`
- `CryptoExchanges.Net.0.5.0-preview.2.nupkg`
- `CryptoExchanges.Net.Mcp.0.5.0-preview.2.nupkg`

## Commits

feat(FEAT-008): bump version to 0.5.0-preview.2; promote CHANGELOG [Unreleased] to release heading
