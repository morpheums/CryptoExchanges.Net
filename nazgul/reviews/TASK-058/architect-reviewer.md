---
reviewer: architect-reviewer
task: TASK-058
verdict: APPROVE
---
# Architect Review — TASK-058

## Verdict: APPROVE

## Summary

TASK-058 introduces KuCoin wire DTOs, DeltaMapper mapping profiles, value parsers, and the bespoke `KucoinSymbolMapper` — all correctly scoped to the KuCoin exchange assembly. All 108 unit tests pass, the solution builds with zero warnings under `TreatWarningsAsErrors=true`, and every hard architectural invariant is satisfied.

## Findings

### Finding: ServerTimeDto shape does not match KuCoin wire format
- **Severity**: LOW
- **Confidence**: 55
- **File**: src/CryptoExchanges.Net.Kucoin/Dtos/ServerTimeDto.cs:4-9
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `ServerTimeDto` is a single-property record with `[JsonPropertyName("data")] public long Data`. KuCoin's `/api/v1/timestamp` returns `{"code":"200000","data":1700000000000}` where `data` is a raw `long`. The service layer should deserialize this as `ResponseDto<long>` directly, not `ResponseDto<ServerTimeDto>` — using the latter would require the wire shape to be `{"data": {"data": ...}}` which KuCoin does not emit. The DTO is currently unreferenced (no service exists yet), so the defect is latent. If it is used as `ResponseDto<ServerTimeDto>` in the upcoming service task, timestamp sync will silently return 0 (the default).
- **Fix**: When implementing the server-time service method, use `ResponseDto<long>` rather than `ResponseDto<ServerTimeDto>`. Alternatively, rename `ServerTimeDto` to clarify it is not a wrapper (or remove it in favour of `ResponseDto<long>`). The implementing task (not this one) should resolve this.
- **Pattern reference**: src/CryptoExchanges.Net.Kucoin/Dtos/ResponseDto.cs — the envelope pattern; `ResponseDto<long>` is sufficient for a scalar `data` payload.

### Finding: KucoinValueParsers is a static class holding pure-helper behavior — acceptable under Invariant 11
- **Severity**: LOW
- **Confidence**: 15
- **File**: src/CryptoExchanges.Net.Kucoin/Internal/KucoinValueParsers.cs:8
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: `KucoinValueParsers` is `internal static class`. Invariant 11 mandates interfaces over static classes for swappable behavior, but allows `static` for "genuinely fixed pure helpers." Decimal/enum/timestamp parsing from a fixed wire format is not swappable behavior — it is pure deterministic conversion. This is the same pattern as the established `BinanceValueParsers` / `OkxValueParsers` in sibling assemblies. No interface is required.
- **Fix**: N/A — pattern is correct.
- **Pattern reference**: src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs (equivalent pattern in Binance assembly)

## Checklist
- [x] 4-layer chain preserved (KuCoin → Core + Http only): `CryptoExchanges.Net.Kucoin.csproj` has `ProjectReference` only to Core and Http; no reverse or cross-exchange references.
- [x] K1: No Core.Models/DeltaMapper in Http layer: diff touches only `src/CryptoExchanges.Net.Kucoin/` and `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/`. No Http layer files modified.
- [x] DeltaMapper mandate: no hand-rolled DTO→model mapping: All DTO→model projections go through `KucoinResponseProfile : Profile` using `CreateMap<>` / `ForMember`. CandlestickDto, TradeDto, and OrderBookDto are intentionally excluded per the approved exception (non-standard array wire shapes handled inline in service code, matching the OKX precedent).
- [x] One type per file: All 16 new `.cs` files contain exactly one top-level type. Verified programmatically — no file has more than one `internal`/`public` type declaration.
- [x] Wire DTOs internal: All 12 DTO types are `internal sealed record`. Confirmed by `grep`.
- [x] ISymbolMapper bespoke (not DeltaMapper): `KucoinSymbolMapper` implements `ISymbolMapper` directly; no DeltaMapper profile is used for symbol translation.
- [x] Mapping profile in Exchange layer: `KucoinMappingProfiles.cs` lives in `src/CryptoExchanges.Net.Kucoin/Mapping/`, not Core or Http.
