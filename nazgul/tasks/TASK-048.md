---
id: TASK-048
status: DONE
commit: 9ce2fcc
claimed_at: 2026-06-19
---

# TASK-048: Trim low-value streaming tests (lean pass)

**Status**: READY

**Blast radius**: NONE (test-only deletions; no production code change).

## Scope
Remove streaming tests that exercise the language/compiler rather than our behavior — record equality, enum membership, default-value, and trivial record-construction tests — keeping the behavior-bearing ones (engine reconnect/backpressure/heartbeat/FIFO/lifecycle, client routing/symbol-mapping, registration, real fake-transport behavior). Target: ~77 streaming tests → ~40 meaningful ones, no real coverage loss.

Files: `tests/CryptoExchanges.Net.Core.Tests.Unit/Streaming/StreamHandlersTests.cs`, `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamContractTests.cs` (primary); light trim of duplicate cases in `StreamClientTests.cs`/`StreamEngineTests.cs`.

## Acceptance
- Build 0W/0E; remaining tests pass; behavior coverage (engine/client/registration) intact.
