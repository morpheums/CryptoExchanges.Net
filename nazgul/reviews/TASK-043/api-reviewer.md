# API Review — TASK-043

## Verdict: APPROVED

## Public Surface Added

No new public types were added to the library's NuGet-visible API surface. All new production types in `src/CryptoExchanges.Net.Http/Streaming/` carry `internal` visibility:

- `internal enum FrameKind`
- `internal readonly record struct StreamFrame`
- `internal enum HeartbeatDirection`
- `internal enum PingFormat`
- `internal sealed record HeartbeatPolicy`
- `internal enum StreamKind`
- `internal sealed record StreamRequest`
- `internal interface IStreamProtocol`
- `internal interface IWebSocketConnection`
- `internal sealed class StreamDecoderRegistry`

The only `public` type added is `FakeWebSocketConnection` in the test project (`tests/CryptoExchanges.Net.Http.Tests.Unit/`), which is correct — test doubles must be `public` to satisfy the `internal interface IWebSocketConnection` via the `InternalsVisibleTo` grant already present in `CryptoExchanges.Net.Http.csproj:12`.

The Core REST interfaces (`IExchangeClient`, `IMarketDataService`, `ITradingService`, `IAccountService`) are confirmed untouched by this commit.

## Findings

### Finding: `StreamDecoderRegistry` — public members on an internal class
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamDecoderRegistry.cs:35,49,61`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `Register`, `Resolve`, and `Contains` are declared `public` on a class that is `internal sealed`. In C# this is idiomatic — the members are accessible only to callers that can see the type (i.e. the Http assembly and its `InternalsVisibleTo` friends). There is no NuGet-visible leakage.
- **Fix**: No action required. The pattern is normal and correct.
- **Pattern reference**: N/A — standard C# encapsulation; `internal` on the type is the binding constraint.

### Finding: `StreamKind` — internal enum referenced by exchange packages in TASK-046
- **Severity**: MEDIUM
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamKind.cs:9`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `StreamKind` is `internal` in `CryptoExchanges.Net.Http`. Exchange packages (Binance, Bybit, OKX, Bitget) will need to reference this enum in TASK-046 when they build and register `StreamDecoderRegistry` closures and implement `IStreamProtocol.BuildSubscribe/BuildUnsubscribe`. The `InternalsVisibleTo` grants in `CryptoExchanges.Net.Http.csproj` already cover all four exchange packages (lines 8-11), so the access chain works today. No breaking change, and the architectural decision is sound. However, if a fifth exchange package is added in the future, the Http `.csproj` must be updated simultaneously — this coupling is worth noting.
- **Fix**: No action required for this task. When TASK-046 adds a new exchange package, ensure `CryptoExchanges.Net.Http.csproj` gets the corresponding `InternalsVisibleTo` entry. Consider adding a code comment in the `.csproj` documenting why these entries exist (exchange packages need Http internals to register streaming closures).
- **Pattern reference**: `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj:8-12`

### Finding: `StreamKind` and `StreamDecoderRegistry` not in original TASK-043 type list
- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamKind.cs`, `StreamDecoderRegistry.cs`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: The TASK-043 spec listed 7 types to create; the implementation added 9 (including `StreamKind` and `StreamDecoderRegistry`). However, `StreamKind` is the "stream-type discriminator token (ticker/trade/orderbook/kline)" that the spec explicitly required `StreamRequest` to carry — the type was always implied. `StreamDecoderRegistry` is the "decode registry" that the spec referenced multiple times as `StreamDecoderRegistry`. Both additions are logically mandated by the spec text and binding constraint K1. The implementation log in TASK-043.md documents both types. No concern.
- **Fix**: No action required.
- **Pattern reference**: TASK-043.md lines 46-49 (discriminator token language), lines 350-360 (K1 binding constraint).

### Finding: `FakeWebSocketConnection` — `public sealed class` in test project
- **Severity**: LOW
- **Confidence**: 95
- **File**: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs:13`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `FakeWebSocketConnection` is `public` in the test project. This is correct: it implements `internal interface IWebSocketConnection` from the Http production assembly, which is accessible to the test project via `InternalsVisibleTo`. The test project is marked `<IsPackable>false</IsPackable>`, so this class will never appear in a NuGet package.
- **Fix**: No action required.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj:12` (InternalsVisibleTo for test project).

### Finding: REST surface — `IExchangeClient`, `IMarketDataService`, `ITradingService`, `IAccountService` untouched
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Interfaces/`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: The diff and commit SHA `547f2f8` show zero changes to any Core interface. All REST public contracts are confirmed unmodified.
- **Fix**: No action required.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Interfaces/IExchangeClient.cs`

### Finding: K1 binding constraint — no Core.Models or DeltaMapper references in Http
- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `grep -rn "Core.Models|DeltaMapper|IMapper" src/CryptoExchanges.Net.Http/ --include="*.cs"` returns empty. The Http layer uses only `byte`, `string`, `Uri`, `Func`, `ReadOnlyMemory<byte>`, `ReadOnlySpan<byte>`, and `System.Net.WebSockets` primitives. K1 is intact.
- **Fix**: No action required.
- **Pattern reference**: TASK-043 Acceptance Criteria §K1 verified.

### Finding: `HeartbeatPolicy` — pure data, no behavioral methods (C1 compliance)
- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/HeartbeatPolicy.cs:31-56`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `HeartbeatPolicy` is a `sealed record` with five positional parameters and no methods, timers, threads, or behavioral logic. C1 (protocol describes, engine executes) is satisfied.
- **Fix**: No action required.
- **Pattern reference**: TASK-043.md line 43 (C1 — pure data).

### Finding: `IStreamProtocol` and `IWebSocketConnection` — correctly `internal`
- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:21`, `IWebSocketConnection.cs:21`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: Both interfaces are `internal`. They are not part of the library's public NuGet surface and cannot be implemented by external consumers. This is the correct and required visibility for engine-internal seam types.
- **Fix**: No action required.
- **Pattern reference**: TASK-043.md lines 50-58 (internal specified for both).

### Finding: Namespace correctness
- **Severity**: LOW
- **Confidence**: 99
- **File**: All new files under `src/CryptoExchanges.Net.Http/Streaming/`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: All 10 new production files use `namespace CryptoExchanges.Net.Http.Streaming;` — consistent with the physical folder structure and existing Http project conventions.
- **Fix**: No action required.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:1` (existing namespace pattern).

## Summary

- PASS: `IStreamProtocol` is `internal` — correctly scoped, not in NuGet surface
- PASS: `IWebSocketConnection` is `internal` — correctly scoped, not in NuGet surface
- PASS: `StreamDecoderRegistry` is `internal sealed class` — public members are encapsulation-safe
- PASS: `StreamKind` is `internal` — exchange packages access it via existing `InternalsVisibleTo` grants
- PASS: `FakeWebSocketConnection` is `public` in test project — correct (implements internal interface via `InternalsVisibleTo`; test project is non-packable)
- PASS: K1 binding constraint — zero `Core.Models`/`DeltaMapper` references in `src/CryptoExchanges.Net.Http/`
- PASS: C1 binding constraint — `HeartbeatPolicy` is pure data, no behavioral methods
- PASS: Core REST interfaces (`IExchangeClient`, `IMarketDataService`, `ITradingService`, `IAccountService`) — confirmed untouched
- PASS: No new NuGet packages introduced — no new `.csproj` files in `src/`
- PASS: Namespaces correct — all new types use `CryptoExchanges.Net.Http.Streaming`
- CONCERN: `StreamKind` internal enum will require `InternalsVisibleTo` maintenance when new exchange packages are added in TASK-046+ (confidence: 70/100, non-blocking)
