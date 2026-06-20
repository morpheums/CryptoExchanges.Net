---
id: TASK-053
status: IMPLEMENTED
---

# TASK-053: NuGet listing polish — transparent icon, markdown NuGet README, badge fix (v0.3.0-preview.2)

**Status**: IMPLEMENTED

**Blast radius**: LOW — packaging/docs only. No product code change. Ships as 0.3.0-preview.2
(0.3.0-preview.1 is already public and immutable).

## Findings (from the live listing)
1. Package icon had a baked white background → looks bad on NuGet dark theme. Make it transparent.
2. The README banner shows as raw HTML text on NuGet — NuGet does not render raw HTML in READMEs.
   Fix with a dedicated **markdown** `NUGET_README.md` (markdown-image banner) packed as the NuGet readme,
   mirroring DeltaMapper; keep the HTML-rich `README.md` for GitHub.
3. README NuGet badge pointed to the org profile, not a package → repoint to the package (live version badge).

## Changes
- `icon.png` → transparent (flood-filled white bg), still wired via `<PackageIcon>`.
- `NUGET_README.md` (new, markdown only) + `Directory.Build.props`: pack it as `/README.md` for the 7
  libraries (MCP keeps its own).
- `Directory.Build.props`: version `0.3.0-preview.1` → `0.3.0-preview.2`.
- `README.md`: NuGet badge → live `shields.io/nuget/v/...` linking to the package.

## Acceptance
- `dotnet pack` → 8 packages, icon.png transparent + present, NUGET_README packed as README.md; 0 errors.
- Merge, then tag `v0.3.0-preview.2` to publish.

## Commits
- `a309af3` — transparent icon + markdown NUGET_README + package badge + version 0.3.0-preview.2.
