---
id: TASK-026
status: IMPLEMENTED
commit: d39c545
claimed_at: 2026-06-19
---

# TASK-026: Canonicalize DTO names across all exchanges (architect ruling)

**Status**: IN_PROGRESS

**Blast radius**: LOW — rename of `internal` wire DTOs only. No public API, no behavior
change. 455-test suite is the regression net; build must stay 0W/0E.

## Scope (folds into PR #17, branch `chore/cleanup-file-per-type`)
Per architect ruling (`nazgul/reviews/DECISION-DTO-NAMING/architect-reviewer.md`): every
leaf DTO is `{CanonicalConcept}Dto`, identical across exchanges. Strip `Response`/`Result`
(banned on leaves). Reserved wrappers: `ResponseDto<T>`/`ResponseObjectDto<T>` (envelope),
`ListDto<T>` (`{list:[...]}`). Canonical vocabulary: `FillDto` (not Execution),
`SymbolInfoDto` (not Instrument/Symbol). Two ratified edge cases: Binance
`TradeHistoryResponseDto → FillDto`; balance containers → `AccountDto` uniformly.

Renames:
- **Binance:** TickerResponseDto→TickerDto, OrderResponseDto→OrderDto, TradeResponseDto→TradeDto,
  OrderBookResponseDto→OrderBookDto, ServerTimeResponseDto→ServerTimeDto, PriceResponseDto→PriceDto,
  ExchangeInfoResponseDto→ExchangeInfoDto, AccountResponseDto→AccountDto, TradeHistoryResponseDto→FillDto.
- **Bybit:** ExecutionDto→FillDto, InstrumentDto→SymbolInfoDto, CoinBalanceDto→BalanceDto,
  WalletAccountDto→AccountDto, OrderBookResultDto→OrderBookDto, ServerTimeResultDto→ServerTimeDto,
  ListResultDto<T>→ListDto<T>, delete TickerResultDto (→ ListDto<TickerDto>).
- **Okx:** InstrumentDto→SymbolInfoDto, BalanceDetailDto→BalanceDto, BalanceAccountDto→AccountDto.
- **Bitget:** SymbolDto→SymbolInfoDto, ObjectResponseDto<T>→ResponseObjectDto<T>.

## Acceptance
- Build 0W/0E (Release); all 455 tests pass.
- Shared concepts identical across all four exchanges; no `Response`/`Result` on leaves;
  no exchange prefix; no public API change.
