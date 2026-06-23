APPROVED

# API Reviewer — TASK-071 (FEAT-008): Outbound Control-Frame Throttling + Serialisation

## Scope of Review

Diff touches four files:
- `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs` — adds `MinOutboundInterval` as a third positional parameter with `= default`
- `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs` — adds `_sendSemaphore`, `SendControlAsync`, `ApplyConnectionPacing`, routes all outbound sends; removes `volatile` from `_livenessFlag`
- `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` — passes `MinOutboundInterval: TimeSpan.FromMilliseconds(200)` to `StreamConnectionInfo`
- `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs` — passes `MinOutboundInterval: TimeSpan.FromMilliseconds(100)` to `StreamConnectionInfo`
- `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs` — 5 new tests + `RecordingWebSocketConnection` helper + `BuildEngineWith` helper

---

## Visibility Audit — Zero Public API Change

**StreamConnectionInfo** (`StreamConnectionInfo.cs:31`): `internal sealed record` — unchanged. NuGet package surface unaffected.

**StreamEngine** (`StreamEngine.cs:40`): `internal sealed class` — unchanged. NuGet package surface unaffected.

**IStreamProtocol** (`IStreamProtocol.cs:21`): `internal interface` — NOT touched by this diff. No implementer breakage.

**StreamEngineOptions** (`StreamEngineOptions.cs:9`): `internal sealed class` — NOT touched by this diff.

**IStreamClient** (`Core/Interfaces/IStreamClient.cs:13`): `public interface` — NOT touched by this diff.

**BinanceStreamProtocol** (`BinanceStreamProtocol.cs:12`): `internal sealed class` — unchanged visibility.

**KucoinStreamProtocol** (`KucoinStreamProtocol.cs:12`): `internal sealed class` — unchanged visibility.

All changed types are `internal`. No public NuGet/SemVer impact. This is an internal implementation change with zero public API surface change.

---

## Findings

### Finding: MinOutboundInterval — third positional param with default preserves source compatibility
- **Severity**: LOW
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs:31-34`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: Adding a third positional parameter to a positional record changes the generated primary constructor signature. However: (a) the type is `internal sealed record`, so no external callers exist; (b) within the assembly, call sites in `BinanceStreamProtocol` and `KucoinStreamProtocol` now pass the third argument explicitly with a named parameter (`MinOutboundInterval:`); (c) `FakeStreamProtocol` passes it positionally. The default `= default` (i.e., `TimeSpan.Zero`) means any two-arg caller that was compiled against the old type compiles cleanly — but since this is `internal`, all callers are in-repo and all have been updated in the same diff.
- **Fix**: No fix required. All in-repo callers accounted for.
- **Pattern reference**: Consistent with `HeartbeatPolicy` venue-property precedent in `HeartbeatPolicy.cs`.

### Finding: XML param doc present on new parameter
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs:24-30`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `<param name="MinOutboundInterval">` is fully documented with semantics, default meaning, and design rationale (venue property, not consumer setting).
- **Fix**: No fix required.
- **Pattern reference**: `IStreamProtocol.cs:46` uses the same `<param>` style.

### Finding: IStreamProtocol unchanged — no new member added to internal interface
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` (not in diff)
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: `MinOutboundInterval` is surfaced via the existing `StreamConnectionInfo` return value of `ResolveConnectionAsync`, not as a new interface method. Existing implementers of `IStreamProtocol` that return a two-arg `StreamConnectionInfo` compile unchanged (default `TimeSpan.Zero` = unthrottled).
- **Fix**: No fix required.

### Finding: volatile removal on _livenessFlag
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:136`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `volatile` was removed from `private int _livenessFlag`. All reads and writes now go through `Interlocked.Exchange` / `Interlocked.CompareExchange`. `Interlocked` operations carry full memory barriers on all .NET-supported architectures, making `volatile` redundant (and technically incorrect to combine with `Interlocked` on some JIT implementations). This is a correctness improvement, not a regression.
- **Fix**: No fix required.

### Finding: _sendSemaphore properly disposed
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:884`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: The new `_sendSemaphore` field is disposed in the `DisposeAsync` finalisation block alongside `_gate`. No resource leak.
- **Fix**: No fix required.

### Finding: SendControlAsync cancel-via-linked-CTS on dispose
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:164-188`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `SendControlAsync` creates a `CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token)` so that a dispose mid-pacing-delay cancels cleanly without leaving an unobserved exception. The test `Engine_Throttle_DisposeDuringDelay_CompletesCleanly` verifies this. The `using` on the linked CTS ensures no CTS leak.
- **Fix**: No fix required.

### Finding: LR-004 applicability — no array parameter introduced
- **Severity**: N/A
- **Confidence**: 99
- **File**: (all changed files)
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: LR-004 (null + min-length guards before indexed access on array params) is not triggered. `SendControlAsync` takes a `string text` and guards with `ArgumentException.ThrowIfNullOrWhiteSpace(text)` — appropriate for the parameter type. No array or `IReadOnlyList` parameter was introduced.
- **Fix**: No fix required.
- **Rule reference**: LR-004 N/A (no array parameter).

### Finding: Naming consistency with HeartbeatPolicy venue-property precedent
- **Severity**: LOW
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs:31-34`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `MinOutboundInterval` follows the `PascalCase` naming convention and is co-located with `Heartbeat` on `StreamConnectionInfo`. Both are documented as "venue properties" set by the protocol, never by consumers. This is structurally identical to the existing `HeartbeatPolicy`/`HeartbeatDirection` precedent.
- **Fix**: No fix required.

### Finding: NuGet/SemVer impact — explicitly none
- **Severity**: N/A
- **Confidence**: 99
- **File**: (all changed files)
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: All changed types (`StreamConnectionInfo`, `StreamEngine`, `IStreamProtocol`, `StreamEngineOptions`, `BinanceStreamProtocol`, `KucoinStreamProtocol`) are `internal`. The public NuGet package surfaces — `IStreamClient`, `IStreamSubscription`, `StreamHandlers<T>`, `BinanceExchangeClient`, `BinanceOptions`, `AddBinanceExchange`, `AddCryptoExchanges` — are entirely untouched. No version bump is required for this change.
- **Fix**: No fix required.

---

## Summary

- PASS: MinOutboundInterval — internal-only third positional param with `= default`; all in-repo callers updated; zero public API delta.
- PASS: XML doc on new param — fully documented with semantics and design rationale.
- PASS: IStreamProtocol unchanged — new parameter piggybacks on existing return value; no implementer breakage.
- PASS: volatile removal on _livenessFlag — Interlocked operations carry full barriers; redundant volatile correctly removed.
- PASS: _sendSemaphore disposal — properly released in DisposeAsync path.
- PASS: SendControlAsync cancel-on-dispose — linked CTS pattern prevents unobserved exceptions; verified by test.
- PASS: LR-004 — N/A; no array parameter introduced; string param guarded with ThrowIfNullOrWhiteSpace.
- PASS: Naming — consistent with HeartbeatPolicy venue-property pattern.
- PASS: NuGet/SemVer — zero public surface change; no version bump required.

---

## Final Verdict

APPROVED — All changed types are `internal`; no public API surface was modified. The `MinOutboundInterval` addition is a clean additive change to an internal record with a sound default (`TimeSpan.Zero` = unthrottled, preserving prior behaviour). All outbound send paths are routed through `SendControlAsync`; `_sendSemaphore` is correctly disposed; dispose-mid-throttle is safely cancelled via linked CTS. Five targeted tests cover throttling, serialisation, zero-interval, reconnect-replay pacing, and dispose-during-delay. No blocking issues.
