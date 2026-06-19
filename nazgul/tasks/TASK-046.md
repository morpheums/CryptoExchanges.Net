---
id: TASK-046
status: DONE
depends_on: [TASK-045]
---
# TASK-046: Exchange-#1 streaming package (protocol + 4 decode closures + options + `Add…Streams`)

## Metadata
- **ID**: TASK-046
- **Group**: 5
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-045
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs, src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamOptions.cs, src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs, src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamTickerDto.cs, src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamTradeDto.cs, src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamDepthDto.cs, src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamKlineDto.cs, src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs, src/CryptoExchanges.Net.Binance/Mapping/BinanceMappingProfiles.cs, tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDecodeTests.cs, tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamProtocolTests.cs]
- **Wave**: 5
- **Traces to**: FEAT-005 spec §Architecture "Binance package"; design §"The seam" + §"Decode registry" + §"Adding exchange #2"; DECISION-STREAMING-SHARED §1 (per-exchange irreducible minimum), §2, §3
- **Created at**: 2026-06-19T17:20:00Z
- **Claimed at**: 2026-06-19T20:00:00Z
- **Base SHA**: 98b3808fb712ec62cfc7f5a6b103db54fedf3d71
- **Implemented at**: 2026-06-19T20:30:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

First consumer of the shared seam: the exchange-#1 (in-repo `Binance` assembly) streaming package — the
**irreducible per-exchange minimum** (DECISION-STREAMING-SHARED §1). This is where `Core.Models` and
DeltaMapper legitimately live (K1 is an Http-only constraint). One type per file.

Create:

- **`BinanceStreamProtocol : IStreamProtocol`** (`internal sealed`, injected — never `static`, Inv 11):
  `Endpoint`, `BuildSubscribe`/`BuildUnsubscribe` (build the venue subscribe wire text from a
  `StreamRequest`; absorb flat-topic-string vs object body), `Classify(ReadOnlySpan<byte>)` →
  `(FrameKind, RoutingKey)` (skip Ack/Pong, surface Error, route Data; RoutingKey = the stream-type token),
  and `Heartbeat` = a `HeartbeatPolicy` describing **server-ping / client-pong** (`Direction =
  ServerPingClientPong`, control-frame pong). Pure data + classification — NO timers/threads (C1).
- **The 4 per-stream decode closures** (`BinanceStreamDecoders` — builds the `StreamDecoderRegistry`
  entries): each `Func<ReadOnlyMemory<byte>, object>` deserializes the venue stream DTO →
  reuses the existing keyed `IMapper` (DeltaMapper `BinanceResponseProfile`) + the bespoke keyed
  `ISymbolMapper` (wire→domain symbol) → returns a boxed `Core.Models.{Ticker,Trade,OrderBook,Candlestick}`.
  The closures are CAPTURED by the engine as opaque delegates.
- **4 streaming DTOs** under `Dtos/Streaming/` (ticker / trade / depth-update / kline) — the venue push
  payload shapes (distinct from the REST DTOs; the WS frames differ in field names/envelope).
- **`BinanceStreamOptions`** — endpoint base URL + any stream knobs (validatable via `ValidateOnStart`).
- **`StreamServiceCollectionExtensions.AddBinanceStreams(...)`** — the ~5-line delegator calling
  `StreamServiceRegistration.AddStreams<BinanceStreamOptions>` supplying `protocolFactory` +
  `decoderRegistryFactory`. Mirror `ServiceCollectionExtensions.AddBinanceExchange`. Opt-in.

Extend `Mapping/BinanceMappingProfiles.cs` ONLY if a streaming DTO needs new `CreateMap` entries not
already covered (e.g. a WS-specific ticker/kline shape); reuse the existing `Ticker`/`Trade` maps where
the DTO field set matches. Do NOT duplicate mapping that the existing profile already covers.

Tests live in a NEW `tests/CryptoExchanges.Net.Binance.Tests.Unit` project (only an Integration project
exists today — create the Unit project + add it to the solution, mirroring the Okx/Bybit unit test
projects). Feed **canned venue frames** (bytes) through each decode closure and assert the mapped
`Core.Models` values; assert `BinanceStreamProtocol.Classify` skips control frames, surfaces Error, and
routes Data to the right stream-type; assert `BuildSubscribe`/`BuildUnsubscribe` wire text. No network.

## Acceptance Criteria
- [ ] `BinanceStreamProtocol : IStreamProtocol` (`internal sealed`, server-ping/client-pong `HeartbeatPolicy`, pure data+`Classify`, no timers — C1), 4 streaming DTOs, 4 decode closures (DTO + existing DeltaMapper profile + keyed `ISymbolMapper` → `Core.Models`), `BinanceStreamOptions`, and ~5-line `AddBinanceStreams` delegating to `AddStreams<TOptions>` — exist with one type per file.
- [ ] New `CryptoExchanges.Net.Binance.Tests.Unit` project added to the solution; canned-frame decode tests assert mapped `Core.Models.{Ticker,Trade,OrderBook,Candlestick}`, and protocol tests assert `Classify` (skip Ack/Pong, surface Error, route Data) + subscribe/unsubscribe wire text — all with NO network; solution builds 0W/0E.
- [ ] Decode closures reuse the existing keyed `IMapper`/`BinanceResponseProfile` + bespoke `ISymbolMapper` (no hand-rolled mapping DeltaMapper covers); existing 499 tests stay green.

## Pattern Reference
- Per-exchange registration delegator to mirror: `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs` (full file — env defaults, options-name, factories, ~5-line shape).
- DeltaMapper profile reuse + `ISymbolMapper` capture in mapping expressions: `src/CryptoExchanges.Net.Binance/Mapping/BinanceMappingProfiles.cs` (Ticker/Trade maps, `symbolMapper.FromWire`).
- Composer / mapper-construction pattern: `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs` (`CreateMapper`, `ComposeForDi` keyed resolution).
- DTO + value-parser style: `src/CryptoExchanges.Net.Binance/Dtos/{TickerDto,TradeDto,OrderBookDto}.cs` + `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs`.
- Seam to implement: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` + decode-registry shape from `StreamDecoderRegistry.cs` (TASK-043/045). Unit-test project shape: `tests/CryptoExchanges.Net.Okx.Tests.Unit/`.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs
- src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamOptions.cs
- src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs
- src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamTickerDto.cs
- src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamTradeDto.cs
- src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamDepthDto.cs
- src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamKlineDto.cs
- src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs
- tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDecodeTests.cs
- tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamProtocolTests.cs
- tests/CryptoExchanges.Net.Binance.Tests.Unit/CryptoExchanges.Net.Binance.Tests.Unit.csproj

**Modifies**:
- src/CryptoExchanges.Net.Binance/Mapping/BinanceMappingProfiles.cs (only if streaming DTOs need new CreateMap entries; reuse existing maps otherwise)
- CryptoExchanges.Net.sln (add the new Binance unit-test project)

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-005 spec §Architecture "Binance package" + Success criterion "the exchange contributes only protocol+decode+options+registration"
- **TRD Component**: per-exchange streaming seam (design §"The seam", §"Decode registry", §"Adding exchange #2 — the entire template")
- **ADR Reference**: DECISION-STREAMING-SHARED §1 (per-exchange irreducible minimum, optional thin construction-glue static permitted, zero behavior), §2 (rich `HeartbeatPolicy`, server-ping/client-pong), §3 (decode = exchange-side opaque `Func`, reuse keyed `IMapper`/`ISymbolMapper`); Inv 6/11

## Commits

- **27169ea** — feat(FEAT-005): add Binance streaming protocol + decoders + AddBinanceStreams (TASK-046)

## Implementation Log

### Attempt 1

**Files created:**
- `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` — `internal sealed`, server-ping/client-pong HeartbeatPolicy, Classify (combined-stream data/ack/error), BuildSubscribe/BuildUnsubscribe (JSON SUBSCRIBE/UNSUBSCRIBE wire format), no timers (C1)
- `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamOptions.cs` — `StreamBaseUrl` with default combined-stream URL
- `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs` — 4 decode closures: Ticker (DeltaMapper), Trade (hand-map), OrderBook (hand-map), Kline (hand-map); case-sensitive JSON serialization to handle single-char keys
- `src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamTickerDto.cs` — new WS ticker DTO (short field names differ from REST TickerDto)
- `src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamTradeDto.cs` — new WS trade DTO
- `src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamDepthDto.cs` — new WS depth DTO with optional `s` symbol field
- `src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamKlineDto.cs` — new WS kline DTO (2 records: StreamKlineDto + StreamKlineBarDto)
- `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs` — `AddBinanceStreams` ~10-line delegator to `StreamServiceRegistration.AddStreams<BinanceStreamOptions>`
- `tests/CryptoExchanges.Net.Binance.Tests.Unit/` — new unit test project, 14 behavior tests

**Files modified:**
- `src/CryptoExchanges.Net.Binance/Mapping/BinanceMappingProfiles.cs` — added `StreamTickerDto→Ticker` CreateMap
- `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj` — added InternalsVisibleTo (Tests.Unit, DynamicProxyGenAssembly2), CA1308 suppressed (lowercase required for WS token names)
- `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj` — added InternalsVisibleTo for Binance.Tests.Unit
- `CryptoExchanges.Net.sln` — added new unit test project

**DTOs reused vs added:** 4 new streaming DTOs (WS frame shapes differ from REST). Existing REST DTOs (TickerDto, TradeDto, OrderBookDto) NOT reused — short single-char field names differ. Existing DeltaMapper BinanceResponseProfile extended with StreamTickerDto→Ticker. Trade/OrderBook/Kline decoded hand-mapped (same convention as REST, which also hand-maps these).

**Build:** 0W/0E Release. 14 new tests pass. All 558 suite tests pass.

## Review Results

### Attempt 1
<!-- review-gate fills this in -->
