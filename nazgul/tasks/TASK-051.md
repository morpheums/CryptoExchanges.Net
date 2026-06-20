---
id: TASK-051
status: DONE
---

# TASK-051: NuGet publish setup — fix library packaging, version bump, release workflow

**Status**: DONE — merged to `main` via PR #28; reviewed on GitHub.

**Blast radius**: LOW — packaging/release config only. No product code change.

## Scope
First public NuGet release of all 8 packages (Core, Http, Binance, Bybit, OKX, Bitget,
DependencyInjection, Mcp tool).

1. **Fix `NU5039`** — `Directory.Build.props` sets `PackageReadmeFile=README.md` globally but only the
   Mcp project bundles a README; the 7 libraries fail to pack. Pack the repo-root `README.md` into every
   packable library (exclude Mcp, which keeps its own).
2. **Version** → `0.3.0-preview.1` (WebSocket streaming shipped since 0.2.0).
3. **CHANGELOG.md** — first entry documenting the public surface at 0.3.0-preview.1.
4. **`.github/workflows/release.yml`** — on `v*` tag push: build (Release, 0W/0E), test
   (`Category!=Integration`), pack all packable projects, `dotnet nuget push` to nuget.org using the
   `NUGET_API_KEY` repo secret (`--skip-duplicate`). Version derived from the tag.

## Acceptance
- `dotnet pack CryptoExchanges.Net.sln -c Release` produces 8 `.nupkg` (0 errors).
- Release workflow validates (`gh workflow`/yaml lint); full non-integration suite green; no opsec leakage
  in any packed README/metadata.

## Commits
- `9b6260b` — packaging fix (NU5039), version 0.3.0-preview.1, release workflow, CHANGELOG, .gitignore.
- `2c8bc70` — publish under Orodruin Labs org (Authors/Company + URLs).
- PR #28 review: SHA-pin GitHub Actions + `persist-credentials: false` (release.yml + ci.yml); status sync.
