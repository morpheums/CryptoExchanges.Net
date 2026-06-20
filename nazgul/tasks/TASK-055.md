---
id: TASK-055
status: DONE
---

# TASK-055: README — convert supported-exchange package cells to NuGet badges

**Status**: DONE — merged to `main` via PR #33; reviewed on GitHub.

**Blast radius**: NONE (GitHub README only).

## Scope
In the Supported Exchanges table, replace the plain `` `CryptoExchanges.Net.<Exchange>` `` code cells for
the 4 supported exchanges with shields.io NuGet badges (live version) linking to each package's nuget.org
page. Leave "Coming soon" rows as "—".

## Acceptance
- 4 badges link to the correct package pages; table still renders; no other changes.

## Commits
- `b8562c5` — supported-exchange package cells → NuGet version badges.
