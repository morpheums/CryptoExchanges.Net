# Architect Review — TASK-043

## Verdict: APPROVED

## K1 Verification

Command: `grep -rn "Core\.Models\|DeltaMapper" src/CryptoExchanges.Net.Http/ --include="*.cs"`

Result: empty (no output). The Http layer is clean of all Core.Models and DeltaMapper references.

Secondary check: `grep -rn "IMapper\|DeltaMapper" src/CryptoExchanges.Net.Http/ --include="*.cs"` → empty.

No `using CryptoExchanges.Net` directives appear anywhere under `src/CryptoExchanges.Net.Http/Streaming/`.

K1 PASS.

## Build Verification

`dotnet build` result: **Build succeeded. 0 Warning(s), 0 Error(s)** (TreatWarningsAsErrors active).

## Test Verification

`dotnet test tests/CryptoExchanges.Net.Http.Tests.Unit/`: **Passed — 44 tests, 0 failures, 0 skipped.**

---

## Findings

### Finding 1: K1 — No Core.Models / DeltaMapper in Http layer
- **Severity**: HIGH
- **Confidence**: 100
- **Blocking**: No (clean pass)
- **File**: `src/CryptoExchanges.Net.Http/Streaming/` (all files)
- **Verdict**: PASS
- **Description**: Grep returned empty. All streaming contract types operate on `byte`, `string`, `Uri`, `Func<ReadOnlyMemory<byte>, object>`, and `ReadOnlyMemory<byte>` only. The Http layer holds zero knowledge of Core.Models or DeltaMapper.

---

### Finding 2: Decode registry opacity — Func<ReadOnlyMemory<byte>, object> parameter
- **Severity**: HIGH
- **Confidence**: 100
- **Blocking**: No (clean pass)
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamDecoderRegistry.cs:23,35,49`
- **Verdict**: PASS
- **Description**: `StreamDecoderRegistry` uses `Dictionary<StreamKind, Func<ReadOnlyMemory<byte>, object>>` — exactly the locked opaque shape from DECISION-STREAMING-SHARED §3. No Core.Models type parameter. The class is `internal sealed`.

---

### Finding 3: IStreamProtocol shape vs locked design
- **Severity**: HIGH
- **Confidence**: 100
- **Blocking**: No (clean pass)
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs`
- **Verdict**: PASS
- **Description**: All five locked members are present with the exact signatures:
  - `Uri Endpoint { get; }` — PASS
  - `string BuildSubscribe(StreamRequest request)` — PASS
  - `string BuildUnsubscribe(StreamRequest request)` — PASS
  - `StreamFrame Classify(ReadOnlySpan<byte> frame)` — PASS
  - `HeartbeatPolicy Heartbeat { get; }` — PASS
- The interface is `internal` — PASS. No behavioral methods added.

---

### Finding 4: C1 binding — HeartbeatPolicy is pure data
- **Severity**: HIGH
- **Confidence**: 100
- **Blocking**: No (clean pass)
- **File**: `src/CryptoExchanges.Net.Http/Streaming/HeartbeatPolicy.cs`
- **Verdict**: PASS
- **Description**: `HeartbeatPolicy` is a `sealed record` (positional constructor only). No timers, threads, `StartHeartbeat`, or any behavioral method. It holds only: `Direction`, `Interval`, `Timeout`, `ClientPingPayload = default`, `PingFormat = PingFormat.ControlFrame`. Binding constraint C1 (protocol describes, engine executes) is upheld.

---

### Finding 5: IStreamProtocol and IWebSocketConnection are internal
- **Severity**: HIGH
- **Confidence**: 100
- **Blocking**: No (clean pass)
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:21`, `IWebSocketConnection.cs:21`
- **Verdict**: PASS
- **Description**: Both interfaces carry the `internal` access modifier. Neither is `public`. All other streaming types (`FrameKind`, `StreamFrame`, `HeartbeatDirection`, `PingFormat`, `StreamKind`, `StreamRequest`, `HeartbeatPolicy`, `StreamDecoderRegistry`) are similarly `internal`.

---

### Finding 6: Invariant 11 — no static class holding behavior
- **Severity**: HIGH
- **Confidence**: 100
- **Blocking**: No (clean pass)
- **File**: `src/CryptoExchanges.Net.Http/Streaming/` (all files)
- **Verdict**: PASS
- **Description**: Grep for `static` in the Streaming directory returns only an XML comment line in `IStreamProtocol.cs` (a doc comment, not a declaration). No `static class` holding swappable behavior introduced. `ExchangeServiceRegistration` remains the only permitted `static class` (thin DI glue per Inv 11 carve-out).

---

### Finding 7: InternalsVisibleTo for Http test project
- **Severity**: MEDIUM
- **Confidence**: 100
- **Blocking**: No (clean pass)
- **File**: `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj:12`
- **Verdict**: PASS
- **Description**: `<InternalsVisibleTo Include="CryptoExchanges.Net.Http.Tests.Unit" />` is present. The test project can access the `internal` streaming types. All 44 tests (24 existing + 20 new streaming contract tests) pass. Note: task manifest logs 519 total tests across all suites; the Http unit test project specifically passes 44.

---

### Finding 8: FakeWebSocketConnection is public in the test project
- **Severity**: LOW
- **Confidence**: 70
- **Blocking**: No
- **File**: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs:13`
- **Verdict**: CONCERN (non-blocking, confidence 70/100)
- **Description**: `FakeWebSocketConnection` is declared `public sealed class`, which is the typical convention for xUnit test helpers that may be reused across test assemblies. This is not a layering violation — it lives in a test project, not in the production Http assembly. However, since `IWebSocketConnection` is `internal`, `FakeWebSocketConnection` being `public` means the type is visible but its implemented interface is not, which creates a minor discoverability asymmetry. The TASK-044/045 engine tests will also need this fake, so public visibility aids reuse. No action required at this time.
- **Fix**: No change needed. If `FakeWebSocketConnection` needs to be shared into the engine test suite in TASK-044, ensure the test project properly references `CryptoExchanges.Net.Http.Tests.Unit` or consider extracting the fake into a shared test helper. Monitor at TASK-044.

---

### Finding 9: StreamDecoderRegistry — methods are public on an internal sealed class
- **Severity**: LOW
- **Confidence**: 60
- **Blocking**: No
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamDecoderRegistry.cs:35,49,61`
- **Verdict**: PASS (acceptable pattern)
- **Description**: `Register`, `Resolve`, and `Contains` carry `public` access modifiers on an `internal sealed class`. Since the class itself is `internal`, these members are effectively internal to the assembly regardless of their declared accessor. This follows the same convention used in other Http types. No issue.

---

### Finding 10: ExchangeServiceRegistration.cs XML doc comment change
- **Severity**: LOW
- **Confidence**: 100
- **Blocking**: No
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:44-48`
- **Verdict**: PASS
- **Description**: The diff removes "DeltaMapper `IMapper` type" from the `TMapper` XML doc comment and replaces it with the more layering-neutral phrasing "per-exchange mapper type". This is a documentation hygiene change that strengthens the K1 invariant narrative. No functional change. Correct and welcome.

---

## Summary

- PASS: K1 (no Core.Models / DeltaMapper in Http) — grep empty, hard invariant upheld at 100/100
- PASS: Decode registry opacity — `Func<ReadOnlyMemory<byte>, object>`, no Core.Models type param
- PASS: `IStreamProtocol` shape — all five locked members present with exact signatures
- PASS: C1 binding — `HeartbeatPolicy` is pure data, no behavioral methods, no timers
- PASS: `IStreamProtocol` / `IWebSocketConnection` both `internal`
- PASS: All other streaming types (`FrameKind`, `StreamFrame`, `HeartbeatDirection`, `PingFormat`, `StreamKind`, `StreamRequest`, `HeartbeatPolicy`, `StreamDecoderRegistry`) are `internal`
- PASS: No `static class` holding swappable behavior; Inv 11 upheld
- PASS: `InternalsVisibleTo` for `CryptoExchanges.Net.Http.Tests.Unit` already present
- PASS: Build 0W/0E; 44 Http unit tests green
- PASS: XML doc comment on `ExchangeServiceRegistration.cs` de-couples language from DeltaMapper
- CONCERN (70/100, non-blocking): `FakeWebSocketConnection` is `public` in the test project — acceptable for test-helper reuse; monitor at TASK-044 for whether the engine test suite needs to share it, and plan accordingly (reference or extract)

No blocking findings. All invariants and binding constraints verified clean.
