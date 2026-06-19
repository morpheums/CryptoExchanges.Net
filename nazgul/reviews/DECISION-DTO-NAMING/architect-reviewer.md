# Architect Review — DTO Naming Convention Ruling (DECISION-DTO-NAMING)

Type: Architectural DECISION request (naming convention), not a code-diff review.
Scope inspected: `src/CryptoExchanges.Net.{Binance,Bybit,Okx,Bitget}/Dtos/`,
`src/CryptoExchanges.Net.Core/Models/`, `Bybit/Mapping/BybitMappingProfiles.cs`.

## Decision 1 — Strip `Response`/`Result` from leaf DTOs: YES

Rule: every leaf DTO is `{Concept}Dto` where `{Concept}` is the canonical Core.Models
domain noun (`TickerDto`, `OrderDto`, `TradeDto`, `OrderBookDto`, `ServerTimeDto`).
No exchange prefix, no `Response`/`Result`/`History` on leaf DTOs. The `Dto` suffix
already means "raw wire shape"; `Response`/`Result` are transport noise.

Reserved suffixes for the only two genuine wrappers:
- `ResponseDto<T>` / `ResponseObjectDto<T>` — the transport envelope; ONLY use of "Response".
  (Rename Bitget `ObjectResponseDto<T>` -> `ResponseObjectDto<T>` so it sorts/reads as a Response sibling.)
- `ListDto<T>` — a `{ list:[...] }` payload; ONLY use of "List".
  (Rename Bybit `ListResultDto<T>` -> `ListDto<T>`; DELETE typed `TickerResultDto`, use `ListDto<TickerDto>`.)

The word `Result` is BANNED — it ambiguously names both Bybit's `result` envelope field
and leaf records (`OrderBookResultDto`), which is the only real source of confusion.
Collision risk you raised is resolved: Bybit `{list:[ticker]}` becomes `ListDto<TickerDto>`,
never `TickerDto`.

## Decision 2 — Canonical cross-exchange vocabulary: option (b)

For divergent native terms, pick ONE canonical word used in every exchange:
- `FillDto` everywhere (rename Bybit `ExecutionDto`).
- `SymbolInfoDto` everywhere (align Bybit/Okx `InstrumentDto`, Bitget `SymbolDto`) — it maps
  to `Core.Models.SymbolInfo`, so the canonical name is the target type's name.

Principle: DTOs are `internal`; their primary reader is the maintainer diffing four
exchanges, not someone cross-referencing one vendor's docs. DeltaMapper collapses N wire
shapes into one Core.Models vocabulary, so DTO names should already speak the destination
vocabulary (`Map<FillDto, Trade>` reads as near-identity everywhere). Native vendor
vocabulary belongs ONLY in `[JsonPropertyName]` (lossless, correct place), never in the type name.

## House rule (for CLAUDE.md / convention doc)

- Name every DTO `{Concept}Dto` using the CANONICAL Core.Models concept it maps to
  (TickerDto, OrderDto, TradeDto, OrderBookDto, ServerTimeDto, FillDto, SymbolInfoDto, BalanceDto),
  identical across all exchanges regardless of the vendor's term. Vendor vocabulary lives ONLY
  in [JsonPropertyName].
- No exchange prefix, no Response/Result/History on leaf DTOs.
- Reserved wrappers only: ResponseDto<T> / ResponseObjectDto<T> (transport envelope, the ONLY "Response");
  ListDto<T> (a `{list:[...]}` payload, the ONLY "List"). Never write a typed list-wrapper.

## Flagged as actively wrong / confusing

1. `Result` on leaf records (Bybit `OrderBookResultDto`, `ServerTimeResultDto`) -> `OrderBookDto`, `ServerTimeDto`.
2. Bybit `TickerResultDto` and typed list-wrappers — redundant with `ListDto<T>`; delete.
3. Bitget `ObjectResponseDto<T>` -> `ResponseObjectDto<T>`.
4. Vocabulary drift: Bybit `ExecutionDto`/`InstrumentDto`, Bitget `SymbolDto`, Binance `SymbolInfoDto`
   -> canonical `FillDto` and `SymbolInfoDto` everywhere.
5. Binance holdout set (`TickerResponseDto`, `OrderResponseDto`, `ServerTimeResponseDto`,
   `OrderBookResponseDto`, `TradeHistoryResponseDto`) -> strip to `{Concept}Dto`
   (`TradeHistoryResponseDto` drops both History and Response -> `TradeDto`).

KEEP: the `Dto` suffix on element types. Verified load-bearing — mapping profiles do
`CreateMap<TickerDto, Ticker>()` / `CreateMap<OrderDto, Order>()` in the same file, so the
suffix disambiguates against Core.Models. Not decoration.

## Architectural-invariant check
No invariant violations: ruling keeps DTOs `internal` (Invariant 3), keeps DeltaMapper as the
DTO->model path (Invariant 6), keeps `ISymbolMapper` bespoke, and does not alter layering,
public interfaces, or package coupling. Recommendation strengthens cross-exchange consistency
for the 5th-exchange implementer.

## Final Verdict
APPROVED

---

## Supplemental rulings — collision edge cases (follow-up, 2026-06-19)

### Edge Case 1 — Binance dual trade shapes

RULING: `TradeResponseDto → TradeDto` (public market trades); `TradeHistoryResponseDto → FillDto`
(authenticated per-order execution records).

Rationale: `TradeHistoryResponseDto` carries `orderId` and structural fields identical in
concept to Okx/Bitget `FillDto` (authenticated, per-order execution with fee). The DTO name
must reflect what the wire shape IS, not which Core type a current hand-written projection
happens to land on (per `BinanceMappingProfiles.cs:53-57`, that mapping is already explicitly
hand-written outside DeltaMapper, so the name change is fully decoupled from it). `FillDto`
is the canonical cross-exchange word for this concept. Collision eliminated: `TradeDto` =
anonymous market trade; `FillDto` = own-order execution. Two different concepts, two names.

CORRECTS the original ruling (which stated `TradeHistoryResponseDto → TradeDto` and
would have produced a same-assembly collision).

### Edge Case 2 — Balance container (two-level structure)

RULING: the balance container is `AccountDto` uniformly across all exchanges that have the
two-level shape; the per-asset leaf is `BalanceDto` uniformly across all exchanges.

Exact renames:
- Binance `AccountResponseDto`  → `AccountDto`
- Bybit   `WalletAccountDto`    → `AccountDto`
- Okx     `BalanceAccountDto`   → `AccountDto`
- Bybit   `CoinBalanceDto`      → `BalanceDto`
- Okx     `BalanceDetailDto`    → `BalanceDto`
- Bitget  `BalanceDto`          — already correct; no container exists, stays flat.

Rationale: `AccountDto` names the structural role (the account snapshot that owns the
balances array), not a mapped Core type (there is no container Core type). It reads as a
natural parent of `BalanceDto`, creates a consistent `AccountDto` contains `BalanceDto[]`
pattern for every two-level exchange, and Binance's existing name already pointed toward
`Account*`. Naming it after its array-field key (`WalletAccountDto`, `BalanceAccountDto`)
is exchange-native vocabulary, which the convention bans from type names.

