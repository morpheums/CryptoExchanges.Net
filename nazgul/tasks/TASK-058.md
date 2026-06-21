---
id: TASK-058
status: IN_PROGRESS
depends_on: [TASK-056]
---
# TASK-058: Bespoke `ISymbolMapper` + REST wire DTOs + DeltaMapper profiles + value parsers

## Metadata
- **ID**: TASK-058
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-056
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Kucoin/Dtos/TickerDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/OrderBookDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/CandlestickDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/TradeDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/ServerTimeDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/SymbolInfoDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/OrderDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/FillDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/BalanceDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/AccountDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/ResponseDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/ListDto.cs, src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs, src/CryptoExchanges.Net.Kucoin/Internal/KucoinValueParsers.cs, src/CryptoExchanges.Net.Kucoin/Internal/KucoinSymbolMapper.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinSymbolAndMappingTests.cs]
- **Wave**: 2
- **Traces to**: PRD-FEAT-006 AC-1; TRD-FEAT-006 §"Symbol Mapping", §"DeltaMapper Profiles", §"Project Layout"; FEAT-006 spec §"Symbol mapping", §"Mapping", §"Build approach" step 3; TEST-PLAN-FEAT-006 §3, §4, §5
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**: 2026-06-21T10:00:00Z
- **Base SHA**: 60f184af0de06efe199e497d2be2a7d519825286
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Build the data layer: the bespoke `ISymbolMapper` for KuCoin's `BASE-QUOTE` dash format, the internal
wire DTOs, the DeltaMapper DTO→`Core.Models` profiles, and the decimal-as-string value parsers. TDD,
cloning the OKX data layer. Honors the house DTO-naming rule: every wire DTO is the canonical
`{Concept}Dto` name; `[JsonPropertyName]` carries KuCoin's vendor field names. One type per file; all
wire DTOs `internal` under `Dtos/`. Reserved wrappers only: `ResponseDto<T>`/`ResponseObjectDto<T>`
(the `{"code","data"}` envelope) and `ListDto<T>` (a `{...,"items":[...]}` page payload).

Create (under `Dtos/`, one type per file — clone the OKX DTO set, adjusting `[JsonPropertyName]` to
KuCoin field names):
- `TickerDto`, `OrderBookDto`, `CandlestickDto`, `TradeDto`, `ServerTimeDto`, `SymbolInfoDto`,
  `OrderDto`, `FillDto`, `BalanceDto` (per-asset leaf), `AccountDto` (container), `ResponseDto<T>`
  (transport envelope), `ListDto<T>` (paged list payload).

Create:
- **`Internal/KucoinValueParsers.cs`** — clone `OkxValueParsers`: `ParseDecimal(string)` →
  decimal-as-string handling (`""`/null → `0m` or null per the OKX convention), enum parse helpers for
  KuCoin order side/type/status strings.
- **`Internal/KucoinSymbolMapper.cs`** — bespoke `ISymbolMapper` (keyed, `internal sealed`): `ToWire`
  → `BTC-USDT`, `FromWire` → `Symbol(BTC, USDT)`, `IsSupported` reflecting registered spot symbols,
  `ToWire`/resolve throwing `ExchangeException` for unsupported symbols. Mirror the bespoke OKX symbol
  mapper.
- **`Mapping/KucoinMappingProfiles.cs`** — clone `OkxMappingProfiles`: one `IMapper` profile with all
  DTO→`Core.Models` `CreateMap` rules (Ticker, OrderBook, Candlestick, Trade, Order, Fill→Trade,
  Balance→AssetBalance, SymbolInfo). Use DeltaMapper (project mandate — do not hand-roll mapping
  DeltaMapper covers); call `KucoinValueParsers.ParseDecimal` and `ISymbolMapper.FromWire` inside
  mapping closures.

Tests (`KucoinSymbolAndMappingTests.cs`), no network:
- Symbol: `ToWire` → `BTC-USDT`, `FromWire` → `Symbol`, `IsSupported` true for registered, `ToWire`
  throws for unsupported.
- Parsers: `ParseDecimal("123.45")` → `123.45m`; `ParseDecimal("")` → convention value.
- DTO roundtrips: Ticker/OrderBook JSON fixtures → DTO fields populated.
- DeltaMapper: `TickerDto→Ticker` (Symbol via `FromWire`), `OrderDto→Order` (status/side/type enums),
  `BalanceDto→AssetBalance`, `FillDto→Trade`.

## Acceptance Criteria
- [ ] Bespoke `KucoinSymbolMapper : ISymbolMapper` (`BTC-USDT` dash format, `IsSupported`, throws for unsupported) + all `internal` `{Concept}Dto` wire DTOs under `Dtos/` (house naming; vendor names only in `[JsonPropertyName]`; reserved `ResponseDto<T>`/`ListDto<T>` wrappers) + `KucoinValueParsers` exist, one type per file, full XML docs.
- [ ] `KucoinMappingProfiles` maps every DTO→`Core.Models` via DeltaMapper (Ticker/OrderBook/Candlestick/Trade/Order/Fill→Trade/Balance→AssetBalance/SymbolInfo); no hand-rolled mapping DeltaMapper covers; decimal-as-string parsed via `KucoinValueParsers`.
- [ ] `KucoinSymbolAndMappingTests` cover symbol mapping, value parsing, DTO roundtrips, and DeltaMapper DTO→model assertions — all NO network; solution builds 0W/0E; existing non-integration suite stays green.

## Pattern Reference
- DTO set + JsonPropertyName style: `src/CryptoExchanges.Net.Okx/Dtos/*.cs` (TickerDto, OrderBookDto, OrderDto, FillDto, BalanceDto, AccountDto, ResponseDto, ServerTimeDto, SymbolInfoDto, TradeDto).
- Value parsers: `src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs`.
- DeltaMapper profile + ISymbolMapper capture: `src/CryptoExchanges.Net.Okx/Mapping/OkxMappingProfiles.cs`.
- Bespoke symbol mapper: the keyed `ISymbolMapper` registered by `src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs` (symbol-mapper construction) + `src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs`.
- Mapping/parsing tests: `tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxMappingAndServiceTests.cs`.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Kucoin/Dtos/TickerDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/OrderBookDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/CandlestickDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/TradeDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/ServerTimeDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/SymbolInfoDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/OrderDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/FillDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/BalanceDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/AccountDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/ResponseDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/ListDto.cs
- src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs
- src/CryptoExchanges.Net.Kucoin/Internal/KucoinValueParsers.cs
- src/CryptoExchanges.Net.Kucoin/Internal/KucoinSymbolMapper.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinSymbolAndMappingTests.cs

**Modifies**:
- (none — additive)

## Traceability
- **PRD Acceptance Criteria**: AC-1 (canonical Core.Models), AC-7 (no-network unit tests)
- **TRD Component**: §"Symbol Mapping", §"DeltaMapper Profiles", §"Project Layout"
- **ADR Reference**: CLAUDE.md DTO-naming house rule; DeltaMapper mandate (MEMORY: use-deltamapper-for-object-mapping); one-type-per-file

## Commits

<!-- implementer fills SHAs -->

## Implementation Log

## Review Results
