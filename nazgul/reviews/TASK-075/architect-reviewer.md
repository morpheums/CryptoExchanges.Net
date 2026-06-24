# Architect Review — TASK-075 (Retry 1)

## Verdict: APPROVED

---

## F1 Fix Verification

**Yes — the fix is correctly applied and surgically scoped.**

Commit `4fe3ff2` adds exactly the three lines prescribed in the blocking finding:

```csharp
/// <summary>Quote-asset volume (turnover).</summary>
[JsonPropertyName("turnover")]
public string Turnover { get; init; } = "0";
```

The property is placed after `Volume` and before `Interval`, matching the prescribed insertion point exactly. The XML doc comment, `[JsonPropertyName]` value, property type (`string`), default (`"0"`), and accessor (`init`) all match the fix specification and the Binance `StreamKlineBarDto.QuoteVolume` reference pattern. The commit diff touches only `StreamKlineDto.cs` plus two Nazgul bookkeeping files (`plan.md`, `TASK-075.md`) — no scope creep. Build reports 0 warnings and 0 errors with `TreatWarningsAsErrors=true`.

---

## Findings

No new findings. The fix resolves the sole blocking issue from the first review pass without introducing any regressions or side effects.

All architectural invariants checked against the current file state:

- **BybitStreamOptions** (`Streaming/BybitStreamOptions.cs`): `public sealed class`, XML-documented, `string StreamBaseUrl` with `wss://` default, no credentials — mirrors `BinanceStreamOptions` exactly.
- **StreamTickerDto / StreamTradeDto / StreamDepthDto / StreamKlineDto**: all `internal sealed record`, namespace `CryptoExchanges.Net.Bybit.Dtos.Streaming`, one type per file, canonical `{Concept}Dto` names, no Core/Http/DI cross-layer pollution.
- **Dependency direction**: DTOs are pure data shapes with no `using` imports beyond `System.Text.Json.Serialization` (via global using or per-file); no reference to Core.Models or Http types.
- **No new public surface** added to any shared interface.
- **No static mutable fields** introduced.
- **No DeltaMapper profiles** needed for this task (DTOs only; mapping deferred to TASK-078 decoders).

---

## Notes

The five non-blocking concerns from the first review cycle (`consolidated-feedback.md`) remain unresolved by design — they are all `ASK` items deferred to TASK-078 or flagged below threshold. None are blocking:

- **No DTO unit tests**: Still absent. Risk is acknowledged (Nazgul Rule 4). The decision to defer to TASK-078's full decoder suite is the implementer's call; acceptable.
- **`BT` (block-trade) field omission** in `StreamTradeDto`: Confidence 75 < 80; non-blocking. Omission is consistent with `Core.Models.Trade` having no block-trade field.
- **`List<List<string>>` mutability** in `StreamDepthDto`: Confidence 70 < 80; non-blocking. Consistent with Binance's streaming DTO shape.
- **URI scheme validation** for `StreamBaseUrl`: Confidence 55 < 80; enforcement belongs in TASK-078's protocol layer.
- **Spot-only default URL** documentation note: INFO-level; TASK-078 should add the clarifying sentence to the property XML doc if non-spot topics are in scope.

None of the above require action before this task is gated to DONE.
