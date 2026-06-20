---
id: TASK-052
status: DONE
---

# TASK-052: Finish org rebrand — README + LICENSE attribution → Orodruin Labs

**Status**: DONE — merged to `main` via PR #30; reviewed on GitHub.

**Blast radius**: NONE (attribution text only; the README is packed into all NuGet listings).

## Scope
- `README.md`: "Built by [Morpheums](https://github.com/morpheums)." → Orodruin Labs.
- `LICENSE`: "Copyright 2026 Morpheums" → "Copyright 2026 Orodruin Labs".
- `docs/architecture.md`: DeltaMapper link → `github.com/OrodruinLabs/DeltaMapper` (also org-owned).

## Acceptance
- No `morpheums` references remain in README.md / LICENSE / docs; build unaffected; merge before tagging the release.

## Brand assets
- `icon.png` (512×512, 129 KB) — package icon (candlestick-convergence), wired via `<PackageIcon>` +
  pack-include in `Directory.Build.props`; embedded in all 8 packages.
- `docs/assets/banner.png` (2560×800) — README hero composited from the real exchange SVGs in
  `docs/assets/exchanges/` on a dark Orodruin-style background; referenced via absolute raw URL so it
  renders on both GitHub and the NuGet listing. Added a trademark/non-affiliation disclaimer.

## Commits
- `01fc5ff` — README/LICENSE/DeltaMapper-link attribution → Orodruin Labs.
- `ac4e6f7` — package icon + README banner + NuGet badge bump to 0.3.0-preview.1.
