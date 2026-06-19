# Code Review — TASK-043

## Verdict: APPROVED

## Findings

### Finding: Missing ArgumentNullException guard on FakeWebSocketConnection.ConnectAsync — uri parameter
- **Severity**: LOW
- **Confidence**: 55
- **File**: tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs:37-42
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: ConnectAsync(Uri uri, CancellationToken ct) accepts a reference-type Uri parameter but has no ArgumentNullException.ThrowIfNull(uri) guard. The project mandate requires guards on every public/internal method parameter that is a reference type. FakeWebSocketConnection is a test double in the test assembly, so this is low-risk in practice, but it is inconsistent with the mandate.
- **Fix**: Add ArgumentNullException.ThrowIfNull(uri); as the first line of ConnectAsync.
- **Pattern reference**: src/CryptoExchanges.Net.Http/Streaming/StreamDecoderRegistry.cs:37 — ArgumentNullException.ThrowIfNull(decoder)

---

### Finding: HeartbeatPolicy record equality with ReadOnlyMemory — test gap
- **Severity**: LOW
- **Confidence**: 65
- **File**: tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamContractTests.cs:74-88
- **Category**: Testing
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: HeartbeatPolicy_Equality_IsValueBased only tests equality when ClientPingPayload is at its default (ReadOnlyMemory<byte>.Empty). ReadOnlyMemory<byte> equality on a sealed record uses default struct equality semantics — comparing the underlying Memory<T> object reference, not byte contents. Two HeartbeatPolicy instances built from independent byte[] arrays with identical bytes will NOT compare equal. The test name overstates the guarantee.
- **Fix**: Either (a) add a test demonstrating that two HeartbeatPolicy instances with identical byte payloads from different arrays are NOT equal (document this intentional struct-equality behavior), or (b) rename the test to HeartbeatPolicy_Equality_IsValueBased_ForScalarFields.
- **Pattern reference**: No direct codebase pattern — general record-equality hazard with ReadOnlyMemory<T> fields.

---

### Finding: IStreamProtocol, IWebSocketConnection, StreamDecoderRegistry remarks blocks — borderline per LEAN mandate
- **Severity**: LOW
- **Confidence**: 45
- **File**: src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:8-20, IWebSocketConnection.cs:10-19, StreamDecoderRegistry.cs:9-21
- **Category**: Style
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: All three types carry <remarks> blocks. The LEAN mandate flags remarks essays as a defect when they restate the summary or describe the obvious. The IStreamProtocol and IWebSocketConnection remarks add genuine architectural rationale (seam purpose, C1 binding constraint, TASK-044/046 traceability). However, the first paragraph of StreamDecoderRegistry remarks (lines 9-14) repeats the K1 constraint already stated in the <summary> verbatim.
- **Fix**: No action required for IStreamProtocol or IWebSocketConnection. For StreamDecoderRegistry, consider removing the first remarks paragraph that restates the summary; keep only the second paragraph about composition-time registration.
- **Pattern reference**: src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs — <remarks> used for ADR reference and non-obvious behavioral note, not summary restatement.

---

### Finding: Implementation log claims 20 tests; file contains 23
- **Severity**: LOW
- **Confidence**: 90
- **File**: Implementation log in task manifest, line 125
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking — LOW severity despite high confidence)
- **Issue**: The implementation log states "20 unit tests" but the file contains 23 [Fact] methods, all passing. The 3 extra tests (binary frame round-trip, SendPong capture, multi-frame ordered delivery) are valid additions that improve coverage. The count discrepancy is a documentation inaccuracy in the task manifest only.
- **Fix**: No code change needed. Correct the count in the task manifest at DONE-state update.
- **Pattern reference**: N/A

## Summary

- PASS: Build — dotnet build CryptoExchanges.Net.sln produces 0 warnings, 0 errors under TreatWarningsAsErrors=true.
- PASS: All tests — 44 Http unit tests pass (21 pre-existing + 23 new streaming contract tests); all tests across the solution pass with no regressions.
- PASS: K1 constraint — grep finds no Core.Models, DeltaMapper, or IMapper references under src/CryptoExchanges.Net.Http/. Http layer contains only byte/string/Uri/Func/TimeSpan primitives.
- PASS: One type per file — 10 new files in src/CryptoExchanges.Net.Http/Streaming/, each containing exactly one type definition.
- PASS: XML docs — all internal interfaces and internal sealed class have XML doc comments; FakeWebSocketConnection uses /// <inheritdoc/> on interface implementations and standalone <summary> on test-control members.
- PASS: readonly record struct StreamFrame — correct C# pattern; positional, immutable, value equality tested.
- PASS: sealed record HeartbeatPolicy — pure data, no timers/threads/behavior (C1 satisfied).
- PASS: IWebSocketConnection abstraction quality — minimal, transport-only surface; no protocol knowledge. The surface (State, IsOpen, ConnectAsync, SendTextAsync, SendPongAsync, ReceiveAsync, CloseAsync, IAsyncDisposable) is the correct seam for testability.
- PASS: FakeWebSocketConnection controllability — ConcurrentQueue + SemaphoreSlim enables deterministic frame sequencing; SimulateDisconnect returns null as the interface contract specifies; ConnectCount enables reconnect-loop assertions; SentText/SentPongs capture all outbound sends.
- PASS: StreamDecoderRegistry — Register has ArgumentNullException.ThrowIfNull(decoder); Resolve throws InvalidOperationException on unknown key with a diagnostic message; composition-time write then read-only during engine operation, no thread-safety concern.
- PASS: Naming conventions — PascalCase types and methods, _camelCase private fields, Async suffix on async methods, I prefix on interfaces.
- PASS: ConfigureAwait(false) — present on the single await in FakeWebSocketConnection.ReceiveAsync.
- PASS: No catch blocks — no exception swallowing in this PR.
- PASS: InternalsVisibleTo — already present for CryptoExchanges.Net.Http.Tests.Unit in the Http csproj (line 12); no modification needed.
- CONCERN: FakeWebSocketConnection.ConnectAsync — missing ArgumentNullException.ThrowIfNull(uri) (confidence: 55/100, non-blocking).
- CONCERN: HeartbeatPolicy_Equality_IsValueBased test — does not cover ReadOnlyMemory<byte> payload field; test name overstates equality guarantee (confidence: 65/100, non-blocking).
- CONCERN: Remarks density — StreamDecoderRegistry first remarks paragraph is redundant with its summary (confidence: 45/100, non-blocking).
- CONCERN: Implementation log claims 20 tests but 23 exist — documentation-only inaccuracy (confidence: 90/100, LOW severity, non-blocking).

## Final Verdict

**APPROVED** — All blocking criteria pass. Build is clean, tests are green, K1 is satisfied, all locked shapes match the task spec, and the FakeWebSocketConnection seam is correctly designed for deterministic engine testing. The four concerns are all LOW severity or sub-80% confidence and do not block merge.
