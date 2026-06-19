---
id: TASK-047
status: DONE
depends_on: [TASK-046]
---
# TASK-047: Wire the 4 public subscribe methods end-to-end + live integration smoke + docs note

## Metadata
- **ID**: TASK-047
- **Group**: 6
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-046
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Binance/Streaming/BinanceStreams.cs, tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs, docs/streaming.md, README.md]
- **Wave**: 6
- **Traces to**: FEAT-005 spec §Success criteria (live delivery + reconnect/resubscribe verified); design §"Build approach" + §"Testing" (live smoke self-skips); DECISION-STREAMING-SHARED §1 (optional thin `CreateStreams` glue)
- **Created at**: 2026-06-19T17:20:00Z
- **Claimed at**: 2026-06-19T00:00:00Z
- **Base SHA**: 358a8d8
- **Implemented at**: 2026-06-19T00:30:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Close the vertical slice: prove all four public subscribe methods deliver live `Core.Models` over a real
socket for exchange #1, and document the capability.

- **Optional thin construction-glue** (`BinanceStreams.CreateStreams(BinanceStreamOptions) → IStreamClient`):
  the explicitly-permitted `static` carve-out (DECISION-STREAMING-SHARED §1) — container-free parity with
  the REST `Create` path. **Zero behavior**: it only constructs the protocol + decoder registry + transport
  + options and hands them to the shared `StreamClient`/`StreamClientFactory.Create`. If it ever needs a
  branch, it has become behavior and must become an injected type — do NOT let that happen. (If the DI path
  + `AddBinanceStreams` from TASK-046 already fully covers the consumer entry points and a container-free
  glue adds no value, this type may be omitted — note the decision in the implementation log.)
- **Live integration smoke** (`Category=Integration`, **self-skips without connectivity** — match the
  existing Binance integration pattern): subscribe to each of ticker / trade / order-book / kline for a
  liquid symbol; assert at least one mapped `Core.Models` update arrives on each within a timeout; assert
  the subscription reaches `State == Live`. Keep it minimal — the no-network correctness lives in the
  TASK-044/045/046 fake-transport unit tests; this smoke only proves the real wire path + mapping.
- **Docs note** (`docs/streaming.md` + a short README pointer): document the public streaming surface
  (`IStreamClient`, the four subscribe methods, `StreamHandlers<T>`, `IStreamSubscription.State`,
  auto-reconnect/resubscribe being transparent), the opt-in `AddBinanceStreams` registration, and link the
  local design doc by relative path. Accurate to shipped state; no roadmap/strategy leakage beyond the
  shipped streaming capability.

No changes to the REST `IExchangeClient` surface. Opt-in throughout.

## Acceptance Criteria
- [ ] All four public subscribe methods are reachable end-to-end for exchange #1 via DI (`AddBinanceStreams`) and/or the optional thin `CreateStreams` glue (zero behavior); a live integration smoke (`Category=Integration`) subscribes to ticker/trade/order-book/kline, asserts a mapped `Core.Models` update on each + `State == Live`, and **self-skips without connectivity**.
- [ ] `docs/streaming.md` + README pointer document the public surface, opt-in registration, and transparent reconnect/resubscribe, linking the local design doc; no REST-surface changes; solution builds 0W/0E.
- [ ] Existing 499 tests (`Category!=Integration`) stay green; the new integration smoke is excluded from the default `Category!=Integration` run and skips cleanly offline.

## Pattern Reference
- Container-free `Create` glue parity: `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs` (`Create`) + `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs` (`Create` static).
- Live integration test self-skip pattern: `tests/CryptoExchanges.Net.Binance.Tests.Integration/` (existing tests — copy the `Category=Integration` + connectivity self-skip approach).
- DI entry the smoke exercises: `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs` (`AddBinanceStreams`, TASK-046).
- Docs voice + link conventions: `docs/getting-started.md` / `docs/architecture.md`; design to link: `docs/superpowers/specs/2026-06-19-websocket-streaming-v1-design.md`.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Binance/Streaming/BinanceStreams.cs (optional thin construction-glue; omit if DI path fully suffices — record decision in log)
- tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs
- docs/streaming.md

**Modifies**:
- README.md (add a short streaming pointer linking docs/streaming.md)

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-005 spec Success criterion "`IStreamClient` delivers live ticker/trade/order-book/kline `Core.Models` … auto-reconnect + auto-resubscribe verified" + "one live integration smoke (self-skips without connectivity)"
- **TRD Component**: end-to-end public subscribe methods + live smoke (design §"Build approach", §"Testing")
- **ADR Reference**: DECISION-STREAMING-SHARED §1 (optional thin `CreateStreams` construction-glue static permitted, zero behavior); FEAT-005 spec §Scope-In (4 public streams)

## Commits

- `58d5216` — feat(FEAT-005): DI wiring test + integration smoke + docs (TASK-047)

## Implementation Log

### Attempt 1

**BinanceStreams.cs decision**: Omitted. `StreamClientFactory.Create` is already a
static container-free factory on the Http assembly, and `AddBinanceStreams` provides the
full DI path. A `BinanceStreams` wrapper would be zero-behavior thin glue with no
additional consumer value — omission noted per the task manifest carve-out.

**DI unit tests** (`BinanceStreamDiTests.cs` — 3 tests):
- `AddBinanceStreams_ResolvesStreamClientFactory` — verifies factory resolves from DI.
- `AddBinanceStreams_FactoryGetClient_ReturnsBinanceClient` — asserts correct ExchangeId.
- `AddBinanceStreams_AvailableExchanges_ContainsBinance` — asserts Available contains Binance.
All use `await using var sp` (StreamClient is IAsyncDisposable-only; sync Dispose throws).

**Integration smoke tests** (`BinanceStreamSmokeTests.cs` — 4 tests):
- `[Trait("Category","Integration")]` excludes them from the default `--filter` run.
- Each test calls `CheckReachabilityAsync()` (helper, not test method — avoids xUnit1030)
  and calls `Assert.SkipWhen` when offline.
- Subscribes ticker/trade/order-book/kline for BTCUSDT, waits up to 20s for one update,
  asserts `State == Live`.

**Docs**:
- `docs/streaming.md`: IStreamClient, 4 methods, StreamHandlers<T>, IStreamSubscription.State,
  auto-reconnect/resubscribe semantics, AddBinanceStreams DI setup, container-free path note,
  design doc link.
- `README.md`: streaming row added to documentation table.

**Build**: 0W/0E (`dotnet build CryptoExchanges.Net.sln -c Release`).
**Tests**: 517 unit tests pass (Category!=Integration); 3 new DI tests pass in Binance unit suite.

## Review Results

### Attempt 1
<!-- review-gate fills this in -->
