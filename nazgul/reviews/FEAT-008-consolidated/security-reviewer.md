# Security Review — FEAT-008-consolidated

## Scope
TASK-071 (outbound control-frame throttling/serialisation), TASK-072 (batched reconnect-replay), TASK-073 (multi-symbol live integration tests), TASK-074 (Binance combined-stream `data`-envelope unwrap). No signing/secret/credential path is touched anywhere in the diff.

---

## Findings

### Finding: Decoder `data`-unwrap — malformed/missing-`data` frames
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs:68-76`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: `DeserializeData<T>` calls `JsonDocument.ParseValue` (propagates `JsonException` on malformed JSON, `ArgumentException` on empty input). Missing `data` property throws `InvalidOperationException`. Both are non-sensitive, non-leaking throws. The engine pump's per-frame try/catch absorbs them so one bad frame drops silently without killing the pump.
- **Fix**: N/A — pattern is correct.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceErrorTranslator.cs:36-50` (JsonException handled at call boundary)

### Finding: `JsonDocument` disposal in decoder helpers
- **Severity**: LOW
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs:71, 83`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: Both `DeserializeData<T>` and `DeserializeDepth` use `using var doc = JsonDocument.ParseValue(...)`. The `dataProp.Deserialize<T>()` call materialises the DTO before the `using` block exits, so no use-after-dispose. No resource leak.
- **Fix**: N/A.
- **Pattern reference**: N/A (standard BCL pattern)

### Finding: Unbounded allocation risk from oversized frames
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs:68-76`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: `JsonDocument.ParseValue` allocates proportional to JSON size. The engine caps inbound messages at 4 MiB (per project context), so the maximum allocation per frame is bounded before the decoder is ever called. No additional cap is needed inside the decoder.
- **Fix**: N/A.
- **Pattern reference**: Documented in system context: "engine already caps message size at 4 MiB"

### Finding: Untrusted-input injection via `WireSymbolFromStreamToken`
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs:96-103`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: The stream token extracted by `WireSymbolFromStreamToken` originates from the `"stream"` field of the Binance WebSocket envelope, which is a server-controlled string. It is parsed with `IndexOf('@')` and sliced — no shell, SQL, or URI construction; the result is passed to `symbolMapper.FromWire()` which uses an internal lookup table. No injection surface.
- **Fix**: N/A.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs:96-103`

### Finding: Throttle path — deadlock risk between `_gate` and `_sendSemaphore`
- **Severity**: MEDIUM
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:308-360`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: Potential lock-ordering concern: `SubscribeAsync` holds `_gate` while awaiting `SendControlAsync` (which acquires `_sendSemaphore`). The heartbeat ping loop acquires `_sendSemaphore` without holding `_gate`. This is the safe ordering: `_gate` → `_sendSemaphore` always, never the reverse. No deadlock is possible because no code path acquires `_gate` while already holding `_sendSemaphore`.
- **Fix**: N/A — ordering is correct.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:401` (`_sendSemaphore.WaitAsync` inside try, never under `_gate`)

### Finding: Dispose-during-pacing-delay cancellation
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:397-419`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: `SendControlAsync` creates a linked CTS from `ct` and `_disposeCts.Token`. `Task.Delay` is cancelled by that linked token on dispose. `_sendSemaphore.Dispose()` is called in `DisposeAsync` after `_disposeCts` is cancelled, so any waiter on `_sendSemaphore.WaitAsync` will observe `OperationCanceledException` from the linked token before the semaphore is disposed. Clean shutdown path, verified by `Engine_Throttle_DisposeDuringDelay_CompletesCleanly` test.
- **Fix**: N/A.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:935-948` (dispose order)

### Finding: No credential, signing, or secret exposure
- **Severity**: N/A
- **Confidence**: 99
- **File**: Entire diff
- **Category**: Security
- **Verdict**: PASS
- **Issue**: No `ApiKey`, `SecretKey`, HMAC key, `X-MBX-APIKEY` header, signing path, or sensitive field appears anywhere in the diff. No new `ToString()` override, no `[JsonInclude]` on any options class, no new serialization path for credentials.
- **Fix**: N/A.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs` (secret stays confined)

---

## Summary

- PASS: Decoder `data`-unwrap — clear throw on missing/malformed envelope, no info leakage
- PASS: `JsonDocument` disposal — `using var doc` pattern; DTO materialised before block exits; no leak
- PASS: Oversized-frame allocation — engine's 4 MiB pump cap bounds allocation before decoder is reached
- PASS: `WireSymbolFromStreamToken` — server-controlled string used only for internal map lookup, no injection surface
- PASS: Throttle deadlock analysis — `_gate` → `_sendSemaphore` ordering is consistent; no reverse acquisition
- PASS: Dispose-during-pacing-delay — linked CTS ensures clean cancellation; semaphore disposed after cancel signal
- PASS: Credential/secret exposure — no signing or secret path touched anywhere in the diff

---

## Final Verdict: APPROVED
