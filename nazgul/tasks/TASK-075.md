---
status: CHANGES_REQUESTED
---
# TASK-075: Bybit streaming wire DTOs + BybitStreamOptions

## Metadata
- **ID**: TASK-075
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamOptions.cs, src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamTickerDto.cs, src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamTradeDto.cs, src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamDepthDto.cs, src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamKlineDto.cs]
- **Wave**: 1
- **Traces to**: PRD AC#1; TRD "Per-Exchange Variation Points" §1 (XxxStreamOptions) + §4 (Wire DTOs); ADR-009-001
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**: 2026-06-24T08:00:00Z
- **Base SHA**: 2bdd8837891be4c8b57c88918e6d2ed9a452ae90
- **Implemented at**: 2026-06-24T08:05:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 1/3

## Description

Create the Bybit streaming option class and the four internal wire DTOs that the Bybit
stream decoders (TASK-077) will deserialize Bybit v5 public-spot data frames into. This is
the foundation task for the Bybit group — no protocol or decoder logic yet, just the data
shapes and the static-URL options class.

`BybitStreamOptions` is a public `sealed class` with a single `StreamBaseUrl` property
defaulting to `wss://stream.bybit.com/v5/public/spot` (TRD §"Bybit v5 Public Spot"). No
credentials — public streams only. Full XML `<summary>` docs on the type and the property
(TreatWarningsAsErrors + GenerateDocumentationFile are on).

The four DTOs are `internal` records, one type per file, in `Dtos/Streaming/`, named per the
`{Concept}Dto` house rule (canonical Core concept, NOT Bybit's vendor term). Vendor vocabulary
goes ONLY in `[JsonPropertyName]`:
- `StreamTickerDto` → maps to `Ticker` (Bybit `tickers.<SYM>` data: `symbol`, `lastPrice`, `highPrice24h`, `lowPrice24h`, `volume24h`, etc.)
- `StreamTradeDto` → maps to `Trade` (Bybit `publicTrade.<SYM>` data is an array of trade rows: `T` ts, `s` symbol, `S` side, `v` size, `p` price, `i` id; `S=="Sell"` → buyer-maker semantics)
- `StreamDepthDto` → maps to `OrderBook` (Bybit `orderbook.<DEPTH>.<SYM>` data: `s` symbol, `b` bids `[[price,qty],...]`, `a` asks, `u` updateId, `seq`)
- `StreamKlineDto` → maps to `Candlestick` (Bybit `kline.<INTERVAL>.<SYM>` data row: `start`, `open`, `high`, `low`, `close`, `volume`, `turnover`, `interval`, `confirm`)

Model these from the Binance/KuCoin DTO files so the shape, nullability, and decimal-as-string
handling match the existing pattern. Bybit's order-book levels are `[price, qty]` string pairs —
follow the Binance `StreamDepthDto` `[][]`/`PriceLevel` decode shape already in the repo.

### Steps
1. Read the reference DTOs in `src/CryptoExchanges.Net.Binance/Dtos/Streaming/` (all 5 files) and `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/` to copy the exact record style, `JsonPropertyName` usage, and string-decimal conventions.
2. Create `BybitStreamOptions.cs` (public sealed, one `StreamBaseUrl` prop, XML docs, default URL).
3. Create the four DTO files with the Bybit v5 field names in `[JsonPropertyName]` only; type names stay canonical `{Concept}Dto`.
4. Build: `dotnet build CryptoExchanges.Net.sln` → 0W/0E.

## Acceptance Criteria
- [ ] `BybitStreamOptions` is a public sealed class with `StreamBaseUrl` defaulting to `wss://stream.bybit.com/v5/public/spot`, full XML docs.
- [ ] Four internal `{Concept}Dto` records exist under `Dtos/Streaming/` (Ticker/Trade/Depth/Kline), one type per file, Bybit field names only in `[JsonPropertyName]`, matching the Binance DTO style at `src/CryptoExchanges.Net.Binance/Dtos/Streaming/`.
- [ ] `StreamKlineDto` includes a `Turnover` property mapping Bybit v5 `"turnover"` (quote-asset volume): `[JsonPropertyName("turnover")] public string Turnover { get; init; } = "0";`
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0 Warning / 0 Error.

## Pattern Reference
- DTO style + JsonPropertyName + string-decimal handling: `src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamTickerDto.cs`, `StreamTradeDto.cs`, `StreamDepthDto.cs`, `StreamKlineDto.cs`, `StreamKlineBarDto.cs`
- KuCoin variant (different vendor field names, same house rule): `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/`
- Options class: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamOptions.cs`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamOptions.cs
- src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamTickerDto.cs
- src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamTradeDto.cs
- src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamDepthDto.cs
- src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamKlineDto.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #1 (live Bybit Ticker after AddBybitStreams), #2 (all four stream kinds)
- **TRD Component**: Bybit variation points §1 (BybitStreamOptions) + §4 (Dtos/Streaming/)
- **ADR Reference**: ADR-009-001 (clone existing pattern), ADR-009-002 (static endpoint)

## Commits
- a1909f9 feat(FEAT-009): TASK-075 — Bybit streaming wire DTOs + BybitStreamOptions

## Implementation Log

### Attempt 1

Created 5 files under `src/CryptoExchanges.Net.Bybit/`:

- `Streaming/BybitStreamOptions.cs` — public sealed class, `StreamBaseUrl` defaulting to
  `wss://stream.bybit.com/v5/public/spot`, full XML docs. Mirrors `BinanceStreamOptions`.

- `Dtos/Streaming/StreamTickerDto.cs` — internal sealed record; maps Bybit v5 `tickers.<SYM>`
  data frame. Fields: `symbol`, `lastPrice`, `highPrice24h`, `lowPrice24h`, `volume24h`,
  `turnover24h`, `prevPrice24h`, `price24hPcnt` (all string-encoded decimals).

- `Dtos/Streaming/StreamTradeDto.cs` — internal sealed record; one entry from the
  `publicTrade.<SYM>` data array. Fields: `T` → TradeTime (long), `s` → Symbol, `S` → Side,
  `v` → Quantity, `p` → Price, `i` → TradeId. Note: `S=="Sell"` → buyer-maker (documented).

- `Dtos/Streaming/StreamDepthDto.cs` — internal sealed record; maps `orderbook.<DEPTH>.<SYM>`
  data. Fields: `s` → Symbol, `b` → Bids, `a` → Asks (both `List<List<string>>`), `u` → UpdateId
  (long), `seq` → Seq (long).

- `Dtos/Streaming/StreamKlineDto.cs` — internal sealed record; one entry from the
  `kline.<INTERVAL>.<SYM>` data array. Fields: `start` → OpenTime (long), OHLCV strings,
  `interval` (string), `confirm` (bool — true when bar is closed).

Build: 0W/0E. Tests: all green (no regressions — 0 Failed across all suites).
Commit SHA: a1909f9

## Review Results

### Attempt 1

**Gate**: CHANGES_REQUESTED (1/3 retries used)

| Reviewer | Verdict | Blocking Findings |
|----------|---------|-------------------|
| architect-reviewer | APPROVED | none |
| code-reviewer | CHANGES_REQUESTED | F1: Missing `turnover` field in StreamKlineDto (MEDIUM/90) |
| security-reviewer | APPROVED | none |
| api-reviewer | APPROVED | none |

**Blocking fix required** (see `nazgul/reviews/TASK-075/consolidated-feedback.md`):

1. [AUTO-FIX] Add `[JsonPropertyName("turnover")] public string Turnover { get; init; } = "0";`
   to `StreamKlineDto.cs` after the `Volume` property. Bybit v5 kline frames always send this field.
   Reference: `src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamKlineBarDto.cs` (QuoteVolume).
