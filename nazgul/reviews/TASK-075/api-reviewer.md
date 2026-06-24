# api-reviewer — TASK-075

## Verdict: APPROVED

## Findings

| # | Severity | Confidence | File | Finding | Rule reference |
|---|----------|------------|------|---------|----------------|
| 1 | INFO | 90 | `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamOptions.cs:12` | `StreamBaseUrl` defaults to the spot-only endpoint `wss://stream.bybit.com/v5/public/spot`. Bybit v5 also has linear/inverse public endpoints and a private endpoint. If TASK-078's `BybitStreamProtocol` needs to support linear perps (common for BTC/USDT-PERP), callers will have to manually override the URL. This is intentional at this stage (spot-first), but the default value and summary should be verified against TASK-078's protocol target before ship. | LR-007 |
| 2 | INFO | 70 | `src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamKlineDto.cs` | `StreamKlineDto` omits `turnover` (quote volume) — present on Bybit v5 kline wire frames as `"turnover"`. The decoder in TASK-078 may need it for mapping to `Candlestick.QuoteVolume`. Not a public API concern (DTO is internal), but worth noting for the implementer. | N/A |

## Summary

All five files are correct. `BybitStreamOptions` is properly `public sealed class` with a `{ get; set; }` mutable property, mirrors `BinanceStreamOptions` exactly in structure, property name, and `{ get; set; }` accessor pattern. Both class-level and property-level `<summary>` XML docs are present, satisfying the `TreatWarningsAsErrors` + `GenerateDocumentationFile` build settings (noting `CS1591` is suppressed project-wide but the public type is documented regardless). All four DTOs are correctly `internal sealed record` — none leaks into the public surface. No `virtual` members, no `protected` constructors, no unnecessary extensibility points that would constrain future sealed evolution. `InternalsVisibleTo` entries in the csproj are unchanged and limited to test assemblies only. NuGet metadata (`PackageId`, `Description`) is pre-existing and unaffected by this diff. The only forward-looking note (LR-007) is that `StreamBaseUrl`'s default value is spot-specific; TASK-078 must consume it directly (as `BinanceStreamProtocol` does via `options.StreamBaseUrl.TrimEnd('/') + "/stream"`), which the property name and type are already correctly shaped for.
