---
id: TASK-073
status: PLANNED
depends_on: [TASK-071, TASK-072]
---
# TASK-073: Multi-symbol Binance + KuCoin L2 order-book LIVE regression test (reproduces the original burst failure)

## Metadata
- **ID**: TASK-073
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-071, TASK-072
- **Delegates to**: none
- **Files modified**: [tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs]
- **Wave**: 3
- **Traces to**: FEAT-008 objective (config.json) — "Add a multi-symbol Binance+KuCoin L2 order-book regression test asserting at least one book update is delivered"; architect-reviewer advisory (bug confirmation — single-symbol smokes never caught the burst)
- **Created at**: 2026-06-23T22:35:00Z
- **Claimed at**:
- **Base SHA**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Add a **live** multi-symbol regression test for each venue that reproduces the original failure
(N-symbol unpaced burst → PolicyViolation close → infinite reconnect, zero data) and proves the
TASK-071 (throttle) + TASK-072 (batched replay) fix delivers data at scale. Existing single-symbol
smokes pass even on the buggy code, so this multi-symbol test is the actual regression guard.

**Depends on TASK-071 and TASK-072** (the fix must be in place for these to pass).

These are integration tests: `[Trait("Category", "Integration")]`, excluded from the default
`dotnet test --filter 'Category!=Integration'` run, and **self-skip when the endpoint is unreachable**
(reuse the existing `CheckReachabilityAsync` pattern in each test class).

Steps:

1. **`BinanceStreamSmokeTests.cs`** — add `OrderBook_MultiSymbol_LiveStream_DeliversAtLeastOneUpdate`:
   - Build the client via the existing `BuildClient()` helper.
   - Subscribe to L2 order-book streams for **≥ 17 symbols** (a fixed list of liquid USDT pairs;
     reuse the `Symbol`/`Asset` construction style already in the file — e.g. BTC, ETH, BNB, SOL, XRP,
     ADA, DOGE, MATIC, DOT, LTC, LINK, AVAX, TRX, ATOM, UNI, ETC, FIL, … ≥ 17 total). Use
     `SubscribeToOrderBookAsync(symbol, depth: 20, handlers, ct)`.
   - Use a single `TaskCompletionSource<OrderBook>` (or a shared callback that `TrySetResult`s on the
     first delivered book) so the test asserts **≥ 1 book update is delivered** across the multi-symbol
     fan-out and that at least one subscription reaches `StreamConnectionState.Live`.
   - On the buggy pre-fix engine this fan-out would burst, get PolicyViolation-closed, and time out
     with zero updates; with the fix it must receive a book within the receive timeout (use a generous
     `ReceiveTimeout`, e.g. 30 s, since the throttled multi-subscribe takes ~17 × 200 ms ≈ 3.4 s before
     all subscriptions are placed).
   - `await using` all subscriptions (collect handles and dispose them — or dispose the client which
     tears down the engine). Keep the test single `[Fact]`, self-skipping.

2. **`KucoinStreamingSmokeTests.cs`** — add the analogous
   `OrderBook_MultiSymbol_LiveStream_DeliversAtLeastOneUpdate`:
   - Mirror the existing KuCoin streaming smoke setup (DI: `AddKucoinExchange` + `AddKucoinStreams`,
     `IStreamClientFactory.GetClient(ExchangeId.Kucoin)`; copy whatever `BuildClient()` /
     `CheckReachabilityAsync` helpers already exist in this class — read the file first and match it).
   - Subscribe to L2 order-book for **≥ 13 KuCoin symbols** (KuCoin `BTC-USDT`-style pairs).
   - Assert ≥ 1 book update delivered and ≥ 1 subscription reaches `Live`. Generous receive timeout.
   - Self-skip on unreachable endpoint (KuCoin requires bullet-public token negotiation — if the REST
     negotiation endpoint is unreachable, skip).

Constraints: integration-only (`[Trait("Category","Integration")]`); must not run or fail in the
default unit suite; self-skip cleanly offline (no flaky red in CI without network). One `[Fact]` per
venue. No production-code changes in this task — tests only. Build clean under TreatWarningsAsErrors +
AnalysisLevel=latest-all (mind CA2007 in test projects — match the existing smoke files' conventions;
the current integration files do not use ConfigureAwait, so follow their established style for the
project). LEAN comments — one short comment explaining that this is the multi-symbol burst regression.

## Acceptance Criteria
- [ ] `BinanceStreamSmokeTests` has a `[Fact]` `OrderBook_MultiSymbol_LiveStream_DeliversAtLeastOneUpdate` that subscribes to ≥ 17 Binance L2 order-book streams, asserts ≥ 1 book update is delivered and ≥ 1 subscription reaches `StreamConnectionState.Live`, is tagged `[Trait("Category","Integration")]`, and self-skips when the endpoint is unreachable.
- [ ] `KucoinStreamingSmokeTests` has the analogous `[Fact]` subscribing to ≥ 13 KuCoin L2 order-book streams with the same assertions, the same Integration trait, and the same self-skip behavior.
- [ ] Both tests are excluded from `dotnet test --filter 'Category!=Integration'` (that run stays green and unchanged); `dotnet build CryptoExchanges.Net.sln` succeeds 0W/0E.

## Pattern Reference
- Binance single-symbol order-book live smoke to clone/extend: `tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs` — `OrderBook_LiveStream_DeliversAtLeastOneUpdate` (~line 116+), `BuildClient()` (~line 49), `CheckReachabilityAsync()` (~line 30), `Symbol`/`ReceiveTimeout` fields (~line 27), and the `[Trait("Category","Integration")]` class attribute.
- KuCoin streaming smoke harness to mirror: `tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs` (read it first — copy its `BuildClient`/reachability/skip helpers and DI wiring exactly).
- Subscribe API + state assertion: `IStreamClient.SubscribeToOrderBookAsync` and `StreamConnectionState.Live` (used in the existing Binance smoke).

## File Scope

**Creates**:
- (none — append to existing integration test files)

**Modifies**:
- tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs (add multi-symbol L2 `[Fact]`)
- tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs (add multi-symbol L2 `[Fact]`)

**Deletes**:
- (none)

## Traceability
- **Objective**: config.json FEAT-008 — "Add a multi-symbol Binance+KuCoin L2 order-book regression test asserting at least one book update is delivered."
- **Advisory**: `nazgul/reviews/FEAT-008/architect-reviewer.md` §"Bug confirmed" (single-symbol smokes never caught it; multi-symbol fan-out is the reproducer).
- **Constraints**: CLAUDE.md (LEAN comments), integration tests gated by `[Trait("Category","Integration")]` and self-skip, TreatWarningsAsErrors/AnalysisLevel=latest-all.

## Commits

- (pending)

## Implementation Log

- (pending)

## Review Results

- (pending)
