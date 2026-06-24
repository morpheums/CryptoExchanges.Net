# CryptoExchanges.Net — Project Instructions

## Git Convention
- The default branch is `main` (protected). Branch + PR for every change; squash-merge to `main`.

## Code Conventions
- **One type per file.** Every top-level type (record/class/DTO/enum/interface/struct/exception) lives in its own file named after the type. No exchange prefix on file or type names — the namespace carries the exchange.
- **Wire DTOs** are `internal` and live in each exchange's `Dtos/` folder. `Core` is the shared cross-exchange contract (`Core.Models`/`Core.Interfaces`); never add a shared DTO project.
- **DTO naming (house rule).** Name every wire DTO `{Concept}Dto` using the *canonical* `Core.Models` concept it maps to — identical across all exchanges regardless of the vendor's term (`TickerDto`, `OrderDto`, `TradeDto`, `OrderBookDto`, `ServerTimeDto`, `FillDto`, `SymbolInfoDto`, `BalanceDto`, `AccountDto`). Vendor vocabulary lives ONLY in `[JsonPropertyName]`, never in the type name.
  - No `Response`/`Result`/`History` on leaf DTOs. Reserved wrappers only: `ResponseDto<T>` / `ResponseObjectDto<T>` (transport envelope — the ONLY use of "Response") and `ListDto<T>` (a `{ list:[...] }` payload — the ONLY use of "List"). Never write a typed list-wrapper.
  - Two-level balance shapes: per-asset leaf → `BalanceDto`, container → `AccountDto`. An exchange's own executed-trades/fills shape → `FillDto`.
- **Comments & XML docs (LEAN).** No banner / section-separator comments (`// ── X ──`, `#region`, `// === X ===`). No comments that restate the code; keep comments only for non-obvious business logic / exchange quirks. XML docs are a short `<summary>` (plus `<param>`/`<returns>`/`<exception>` only where they add information the signature doesn't) — no `<remarks>`/`<para>` essays. Interface members carry the docs; implementations use `<inheritdoc/>`. When cloning a reference file, do **not** copy its comment noise — clean it to these rules.
