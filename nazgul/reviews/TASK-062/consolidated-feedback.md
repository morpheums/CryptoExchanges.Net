# TASK-062 — Consolidated Review Feedback (Cycle 1)

## Gate decision: CHANGES_REQUESTED (require_all_approve=true)

| Reviewer | Verdict |
|----------|---------|
| architect-reviewer | APPROVE |
| code-reviewer | APPROVE |
| security-reviewer | APPROVE |
| api-reviewer | CHANGES_REQUESTED |

## BLOCKING (must fix) — 1 item

### [AUTO-FIX] `KucoinStreamOptions.RestBaseUrl` is a public option that is silently ignored
- **Source**: api-reviewer REJECT @98% (independently flagged by code-reviewer @85% and architect @90% as concern).
- **Problem**: `KucoinStreamOptions.RestBaseUrl` (default `https://api.kucoin.com`) is public and caller-configurable, registered via `ValidateOnStart`, but the `protocolFactory` in `StreamServiceCollectionExtensions.AddKucoinStreams` never reads `KucoinStreamOptions`. The bullet-public `KucoinBulletPublicClient` is built from the named `"kucoin"` HttpClient whose `BaseAddress` is fixed by `AddKucoinExchange`. A consumer who sets `RestBaseUrl` (e.g. for sandbox) gets a silent no-op — a misleading public API surface.
- **Fix**: Make `RestBaseUrl` actually control the bullet-public base address. In `protocolFactory`, resolve `KucoinStreamOptions` and thread `RestBaseUrl` through to the bullet-public HTTP call so an override is honored. Mirror the Binance pattern (`BinanceStreamServiceCollectionExtensions` resolves its options in `protocolFactory`).
  - Acceptable approach: resolve `sp.GetRequiredService<KucoinStreamOptions>()`; on the freshly-created (per-call) `kucoin` HttpClient, set `BaseAddress` to `RestBaseUrl` before wrapping in `KucoinBulletPublicClient`. (CreateClient returns a new instance each call, so mutating its BaseAddress is isolated.)
  - Add a guard on `RestBaseUrl` (LR-001: `ArgumentException.ThrowIfNullOrWhiteSpace`) at the consumption point, and a `Uri`-validity check.
  - Add a unit test proving a custom `RestBaseUrl` is the host actually used for the bullet-public negotiation (e.g. configure a sandbox URL and assert the outgoing request URI / resolved base).

## NON-BLOCKING (optional, not gating) — for awareness

- code-reviewer / architect: one-type-per-file — `BulletPublicDto.cs` (+`InstanceServerDto`), `StreamDepthDto.cs` (+`DepthChangesDto`), `KucoinBulletPublicClient.cs` (+`IKucoinBulletPublicClient`). Pre-existing cloned pattern; cosmetic.
- code-reviewer: `Classify` `typeProp.GetString()` lacks `ValueKind` guard (70%, pre-existing Binance pattern).
- security-reviewer: OrderBook decoder indexes `b[0]/b[1]/a[0]/a[1]` without `Count>=2` bounds check (75%, depends on engine try/catch).
- api-reviewer: duplicate `"kucoin"` client-name constant; `ValidateOnStart` is a no-op without DataAnnotations.

These are NOT required for this cycle's gate. Fix only the BLOCKING item.
