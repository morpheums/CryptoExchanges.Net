---
reviewer: architect-reviewer
task: TASK-062
cycle: bugfix
verdict: APPROVE
---

# Architect Review — TASK-062 (bugfix re-review, commit 32f75f7)

## Summary of Change

Fixes the KuCoin ticker stream: the subscription channel was wrong (`/market/ticker:{sym}` has a
different wire shape than `/market/snapshot:{sym}`). This caused live integration tests to time out
because the server sends snapshot-shaped frames on the snapshot channel but the DTO expected the
ticker-channel shape (string-encoded fields, different JSON property names, no `data.data` nesting).
The fix: change the topic in `BuildTopic`, replace `StreamTickerDto` with a snapshot-shaped DTO, add
`DeserializeSnapshotData` for the double-nested envelope, update the DeltaMapper profile, and update
all affected tests. Also hardens CI by filtering integration tests out of the build runner.

---

## Finding 1: Channel correctness — routing keyspace consistency

- **Severity**: HIGH
- **Confidence**: 99
- **Blocking**: false (no violation found — this is a PASS)
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs:139-149`

`BuildTopic` is the single source of truth for the topic string. All three consumers — `BuildSubscribe`,
`BuildUnsubscribe`, and `RoutingKeyFor` — call `BuildTopic` directly (no inline string constants). The
`Classify` method reads the `topic` field verbatim from the incoming frame and sets it as `RoutingKey`;
it does not re-construct the topic itself. As a result:

- `RoutingKeyFor` = output of `BuildTopic` = `/market/snapshot:{sym}`
- `Classify` = JSON `topic` field from the frame = `/market/snapshot:{sym}` (what KuCoin sends)
- These two are guaranteed to match by the single-source design.

No keyspace collision: snapshot → `/market/snapshot:*`, trade → `/market/match:*`, order book →
`/market/level2:*`, kline → `/market/candles:*_interval`. All four prefixes are structurally distinct;
no cross-kind collision possible.

The round-trip test `RoutingKeyFor_Ticker_MatchesClassifyRoutingKey` explicitly asserts
`subscribeKey.Should().Be(classifiedKey)` with a live snapshot-envelope frame.

**Verdict: PASS.**

---

## Finding 2: Double-nesting unwrap isolation

- **Severity**: HIGH
- **Confidence**: 97
- **Blocking**: false (no violation found — this is a PASS)
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs:44-48, 119-169`

`DeserializeSnapshotData<T>` is used exclusively for `StreamKind.Ticker`. Trade, OrderBook, and Kline
all call `DeserializeData<T>` (single-nested). The two paths are cleanly separated by the registry
closure per `StreamKind`, so there is no possibility of the snapshot path contaminating other frame
types.

The fallback behavior in `DeserializeSnapshotData`:
1. Full frame (`data` present, `data.data` present) → deserializes inner `data.data` object. Correct.
2. `data` present but no inner `data` → deserializes `outerData` directly. This handles test fixtures
   that supply only a single-level envelope. The fallback is architecturally sound: the else branch
   cannot be triggered by a live KuCoin frame (KuCoin always sends the full double-nested shape on
   `/market/snapshot`) and the test coverage for both paths is present.
3. No envelope at all (bare payload) → falls through to raw `JsonSerializer.Deserialize`. Correct.
4. `JsonException` on the outer parse → falls through to raw deserialize. Correct.

There is no code path by which `DeserializeSnapshotData` could corrupt a Trade/OrderBook/Kline frame:
those decoders are registered under different `StreamKind` keys and call a different method.

**Verdict: PASS.**

---

## Finding 3: K1 constraint — Core.Models and DeltaMapper stay in Kucoin package

- **Severity**: HIGH
- **Confidence**: 100
- **Blocking**: false (no violation found — this is a PASS)
- **File**: commit file list

The diff touches exactly these files:
- `.github/workflows/ci.yml`
- `src/CryptoExchanges.Net.Kucoin/` (5 files)
- `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/` (2 files)

No file in `src/CryptoExchanges.Net.Http/` was modified. No new `using` or `ProjectReference` nodes
were introduced in any Http-layer file. DeltaMapper references remain in `KucoinMappingProfiles.cs`
(Kucoin package only). K1 is intact.

**Verdict: PASS.**

---

## Finding 4: C1 constraint — no timers or threads introduced

- **Severity**: HIGH
- **Confidence**: 100
- **Blocking**: false (no violation found — this is a PASS)
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs`,
  `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs`

`DeserializeSnapshotData` is a synchronous, pure parsing function. `KucoinStreamProtocol` is
unchanged in structure — no `Timer`, `Thread`, `Task.Run`, or background work was introduced. The
`_nextId` field uses `Interlocked.Increment` (pre-existing). C1 holds.

**Verdict: PASS.**

---

## Finding 5: ADR-002 seam — ResolveConnectionAsync delegation unchanged

- **Severity**: MEDIUM
- **Confidence**: 100
- **Blocking**: false (no violation found — this is a PASS)
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs:34-63`

`ResolveConnectionAsync` is untouched by the diff. It still delegates entirely to the injectable
`IKucoinBulletPublicClient.NegotiateAsync`, returning a fresh `StreamConnectionInfo` (new token, new
`connectId`) on every call. The `CountingFakeBulletClient` test confirms the method is called on every
resolve (AC-4, reconnect re-negotiation). Seam is clean.

**Verdict: PASS.**

---

## Finding 6: CI filter change — `--filter 'Category!=Integration'`

- **Severity**: MEDIUM
- **Confidence**: 98
- **Blocking**: false (no violation found — this is a PASS)
- **File**: `.github/workflows/ci.yml:38`

The CI `Test` step now adds `--filter 'Category!=Integration'`. This is consistent with `release.yml`
line 45, which has used the identical filter since the release workflow was authored. Both workflows
now agree: unit tests run on CI/release; integration tests (marked `[Trait("Category", "Integration")]`)
are excluded from automated pipelines that lack live-exchange credentials. The change correctly
prevents KuCoin (and other) streaming integration tests from running on GitHub-hosted runners without
credentials, which was the proximate cause of the timeout failures.

All 87 unit tests pass with the filter applied (confirmed by local `dotnet test` run).

No `Category` trait leakage was observed — unit test classes in both `KucoinStreamProtocolTests` and
`KucoinStreamDecodeTests` carry `[Trait("Category", "Unit")]`, which is not excluded.

**Verdict: PASS.**

---

## Finding 7: Layering — no new cross-layer dependencies

- **Severity**: HIGH
- **Confidence**: 100
- **Blocking**: false (no violation found — this is a PASS)
- **File**: all modified `.cs` files

Namespace scan of all modified files:
- All source files are `CryptoExchanges.Net.Kucoin.*` or `CryptoExchanges.Net.Kucoin.Dtos.*` —
  no Core or Http namespace appears in new `using` statements that was not already there.
- No `ProjectReference` nodes were added or modified.
- `KucoinMappingProfiles.cs` already references `DeltaMapper` and `CryptoExchanges.Net.Core.Models`
  (Kucoin package, allowed). The removed `ParseNsTimestamp` helper was the only change and it was a
  pure simplification.

**Verdict: PASS.**

---

## Finding 8: StreamTickerDto field shape — snapshot wire format alignment (non-blocking concern)

- **Severity**: LOW
- **Confidence**: 75
- **Blocking**: false (CONCERN — confidence below 80)
- **File**: `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTickerDto.cs`

The new `StreamTickerDto` maps `buy`/`sell` for best bid/ask price but does not capture best bid/ask
size (no `buySize`/`sellSize` fields). The KuCoin `/market/snapshot` payload does include size fields.
The `Ticker` core model may or may not expose bid/ask sizes — if it does not expose them today, this
is not a gap, but if a future `Ticker` model extension adds those fields, the DTO will need updating.
This is a pre-existing model limitation, not a regression introduced by this fix. Low priority.

**Recommendation**: No action required now. If `Ticker` ever adds `BestBidSize`/`BestAskSize`,
remember to extend `StreamTickerDto` and the DeltaMapper profile simultaneously.

---

## Build and Test Gate

- `dotnet build` (Release, TreatWarningsAsErrors): 0 errors, 0 warnings. PASS.
- `dotnet test --filter 'Category!=Integration'` (Release): 87/87 passed. PASS.

---

## Summary

| # | Item | Result |
|---|------|--------|
| 1 | Routing keyspace (RoutingKeyFor/Classify/BuildTopic consistency) | PASS |
| 2 | Double-nesting unwrap isolation (snapshot vs Trade/OrderBook/Kline) | PASS |
| 3 | K1 constraint (Core.Models / DeltaMapper stay in Kucoin) | PASS |
| 4 | C1 constraint (no timers or threads) | PASS |
| 5 | ADR-002 seam (ResolveConnectionAsync delegation unchanged) | PASS |
| 6 | CI filter change consistency with release.yml | PASS |
| 7 | Layering — no new cross-layer dependencies | PASS |
| 8 | StreamTickerDto missing bid/ask size fields | CONCERN (low, non-blocking, confidence 75) |

No blocking rejections. All hard REJECT lines are clear.
