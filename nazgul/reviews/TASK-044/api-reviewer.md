# API Review — TASK-044 (Cycle 2)

## Verdict: APPROVED

---

## Prior Blocking Finding Resolution

### FINDING-1 (was BLOCKING): PingFormat.ControlFrame — SendPingAsync semantic fix

RESOLVED. Evidence chain is complete and correct:

1. **Interface** (`src/CryptoExchanges.Net.Http/Streaming/IWebSocketConnection.cs:48-55`): `Task SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)` added, placed immediately before `SendPongAsync` (line 63). XML doc references RFC 6455 §5.5.2 (Ping, opcode 0x09) and correctly distinguishes this from `SendPongAsync` (§5.5.3, Pong). The contract is unambiguous: `SendPingAsync` is for client-initiated heartbeat pings; `SendPongAsync` is for server-ping replies.

2. **Engine routing** (`src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:631-635`): The `PingFormat.ControlFrame` case now calls `_socket.SendPingAsync(policy.ClientPingPayload, ct)`. The inline comment repeats the RFC §5.5.2 citation and explicitly states `SendPongAsync (opcode 0x0A) is reserved for replying to server pings`. The semantic bug is corrected.

3. **Fake** (`tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs:83-86`): `SendPingAsync` enqueues to `SentPings`, not `SentPongs`. Correct.

4. **Test** (`tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs:683-717`): `Engine_HeartbeatClientPing_ControlFrame_SendsPingNotPong` asserts `fake.SentPings.Should().NotBeEmpty(...)` AND `fake.SentPongs.Should().BeEmpty(...)`. Both the positive and the negative assertion are present, constituting a proper regression guard for this exact semantic bug.

---

## Prior Non-Blocking Finding Resolution

### FINDING-2 (was LOW): MaxSubscriptionsPerSocket dead property

RESOLVED. `MaxSubscriptionsPerSocket` returns zero matches across the entire codebase. The property is fully removed from `StreamEngineOptions`. No reserved or dead member remains.

### FINDING-3 (was LOW): [Range] on TimeSpan fields

RESOLVED. `StreamEngineOptions.cs` now carries:
- `[Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]` on `IdleCloseDelay` (line 25), `BackoffInitial` (line 31), `BackoffMax` (line 37).
- `[Range(0, int.MaxValue)]` on `MaxReconnectAttempts` (line 52). Zero as the sentinel for "unlimited" is documented in the XML summary.
- `[Range(1.0, 10.0)]` on `BackoffMultiplier` (line 44).

The 1ms lower bound prevents zero/negative delays. The 1-hour upper bound is a reasonable ceiling for all three TimeSpan fields. `BackoffMax >= BackoffInitial` is not enforced by `[Range]` (each field is validated independently), but the runtime guard in `BackoffSchedule`'s constructor provides a descriptive failure if misconfigured. This failure mode is acceptable.

### FINDING-4 (was LOW): SubscriptionEntry public members

NOT ADDRESSED as a code change — and none was required. `SubscriptionEntry` is `private sealed class` nested inside `internal sealed class StreamEngine`. Its three `public` properties (`Channel`, `WriteFrame`, `Handle`) have no exposure outside the engine. The effective accessibility is `private`. The current state is functionally correct and raises no API surface concern.

---

## New API Surface Assessment

### SendPingAsync contract quality

- **Signature**: `Task SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)` — correct. `ReadOnlyMemory<byte>` is the right parameter type for a binary ping payload. `CancellationToken ct` as the last parameter follows the established pattern on all other interface methods.
- **XML doc**: The summary references RFC 6455 §5.5.2. `SendPongAsync`'s doc references §5.5.3. The pair is coherent and self-documenting. No ambiguity between the two methods.
- **Placement**: `SendPingAsync` at line 55, `SendPongAsync` at line 63 — logically grouped, ping before pong. Correct.
- **Internal consistency**: The interface now has a complete and internally consistent control-frame API.

### FakeWebSocketConnection API surface

- `SentPings: ConcurrentQueue<ReadOnlyMemory<byte>>` at line 39 — thread-safe, correct type.
- `SendPingAsync` at lines 83-86 — enqueues to `SentPings` only. Correct.
- `SentText` upgraded from `List<string>` to `ConcurrentQueue<string>` — fixes a data race in reconnect tests. Internal test-double change only.
- `FakeWebSocketConnection` now implements `IDisposable`. `DisposeAsync` no longer disposes the semaphore (prevents double-dispose on reconnect cycles); `Dispose()` handles final teardown under `_semLock`. The semaphore-recreation pattern in `ConnectAsync` (drain stale frames, create fresh `SemaphoreSlim`, dispose old one) is correctly guarded by `_semLock`. `EnqueueFrame` and `SimulateDisconnect` helpers also lock `_semLock` before capturing the semaphore reference. The fake is a complete, thread-safe, non-leaking test double for the extended interface.

### No new dead or reserved members

No TODO-only stubs, "reserved for v1.1" properties, or unimplemented interface members introduced. `BackoffSchedule` (new in this diff) is fully implemented — `Next()`, `Reset()`, `Attempt` are all functional and covered by tests in `StreamEngineTests.cs:1447-1486`.

### REST surface — confirmed untouched

No modifications to `IMarketDataService`, `ITradingService`, `IAccountService`, `IExchangeClient`, or any other Core interface. The remediation delta is entirely scoped to `Streaming/`.

---

## LR-001 Compliance

LR-001 (string parameter null/whitespace guard) applies to `string` parameters only.

| New method | String params | LR-001 applies | Status |
|---|---|---|---|
| `IWebSocketConnection.SendPingAsync(ReadOnlyMemory<byte>, CancellationToken)` | None | N/A | PASS |

No new string parameters on any new API surface introduced in the remediation diff.

---

## Summary

- PASS: FINDING-1 (was BLOCKING) — `SendPingAsync` added to interface; engine routes `ControlFrame` correctly; fake enqueues to `SentPings`; regression test asserts ping sent and pong not sent.
- PASS: FINDING-2 (was LOW) — `MaxSubscriptionsPerSocket` fully removed.
- PASS: FINDING-3 (was LOW) — `[Range]` attributes added to all four previously unguarded `StreamEngineOptions` fields.
- PASS: FINDING-4 (was LOW) — `SubscriptionEntry` public members have no API surface exposure; no change required.
- PASS: REST surface — untouched.
- PASS: No new dead or reserved members.
- PASS: LR-001 — no new string parameters on new API surface.

## Final Verdict: APPROVED

Confidence: 98/100.

All cycle-1 findings are resolved. The remediation is correct, complete, and introduces no new issues. The `SendPingAsync` contract is clean, the fake is a non-leaking test double, and the regression test provides direct evidence of the fix.
