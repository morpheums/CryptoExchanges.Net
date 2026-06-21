---
reviewer: security-reviewer
task: TASK-062
cycle: bugfix
verdict: APPROVE
---

# TASK-062 Security Review — Bugfix Cycle (commit 32f75f7)

## Scope

This is a re-review of the ticker channel bugfix: switching the subscription topic from
`/market/ticker:{symbol}` to `/market/snapshot:{symbol}` and updating the decoder to
handle the double-nested `data.data` envelope that the snapshot channel uses.

---

## Findings

### Finding: JSON parsing safety in `DeserializeSnapshotData`
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs:143-169`
- **Category**: Security
- **Verdict**: PASS

The JSON document is parsed exactly once via `JsonDocument.ParseValue(ref reader)`. All property
navigation (`TryGetProperty`) operates on the already-parsed `JsonDocument` — no second parse of
user-controlled bytes is performed. The `JsonException` catch covers all paths that return from
within the `try` block, including both `ParseValue` and the subsequent `Deserialize<T>` calls on
`JsonElement` instances. The final fallback `JsonSerializer.Deserialize<T>(frame.Span, JsonOpts)`
outside the try/catch is intentional: if the outer parse threw `JsonException`, the span is treated
as a bare inner payload. `JsonDocument.ParseValue` is bounded by the input size; no unbounded
memory allocation is possible from the input. No security issue.

### Finding: `ValidateWsEndpoint` SSRF guard intact
- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs:180-197`
- **Category**: Security
- **Verdict**: PASS

The diff to `KucoinStreamProtocol.cs` is a single-line change inside `BuildTopic` (line 254 in
the patch, line 141 in the current file): `"/market/ticker:{...}"` → `"/market/snapshot:{...}"`.
`ValidateWsEndpoint` is unchanged. The `wss://` scheme check and `*.kucoin.com` host allowlist
remain fully in force. SSRF guard is not weakened.

### Finding: Decimal overflow risk in `ChangeRate * 100m`
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs:167`
- **Category**: Security
- **Verdict**: PASS

`ChangeRate` is a `decimal` deserialized from a JSON number. KuCoin documents this field as a
fractional rate (e.g. `0.0014` for 0.14%). The `decimal` type has a maximum value of roughly
7.9 × 10^28; multiplying by `100m` is safe for any realistic price-change value. Even extreme
outlier values (e.g. 10x daily change = `10.0`) would produce `1000m`, well within bounds.
`OverflowException` from `decimal` arithmetic requires values exceeding the type's range, which
cannot occur for exchange-sourced price-change rates. No concern.

### Finding: `Datetime > 0` guard on timestamp conversion
- **Severity**: LOW
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs:169-171`
- **Category**: Security
- **Verdict**: PASS

```csharp
.ForMember(d => d.Timestamp, o => o.MapFrom(s => s.Datetime > 0
    ? DateTimeOffset.FromUnixTimeMilliseconds(s.Datetime)
    : (DateTimeOffset?)null))
```

`DateTimeOffset.FromUnixTimeMilliseconds` accepts any `long` within the range
`[DateTimeOffset.MinValue.ToUnixTimeMilliseconds(), DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()]`
(approximately ±9.2 × 10^12 ms). The `> 0` guard prevents a zero or negative millisecond value
from being treated as a valid timestamp (which would produce a date in 1970 or earlier). Negative
values are not reachable from the KuCoin wire (the field is a `long` parsed from a JSON number),
but the guard is a correct defensive practice. No issue.

### Finding: CI integration test exclusion
- **Severity**: LOW
- **Confidence**: 99
- **File**: `.github/workflows/ci.yml:38`
- **Category**: Security
- **Verdict**: PASS

Adding `--filter 'Category!=Integration'` is a security improvement. Integration tests that
connect to live KuCoin WebSocket endpoints would fail on CI runners that lack credentials, and
a failure in test teardown could potentially expose timeout details. Excluding them prevents
CI from requiring live credentials and eliminates any risk of credential-related secrets being
injected via environment variables into CI to satisfy the integration suite. No security regression.

### Finding: No new credential handling or attack surface
- **Severity**: LOW
- **Confidence**: 99
- **File**: all changed files
- **Category**: Security
- **Verdict**: PASS

The diff introduces no new HTTP endpoints, no new credential fields, no new signing paths, no new
external service calls, and no new deserialization of untrusted data outside the existing streaming
frame path. The bullet-public negotiation path is unchanged. `SecretKey` and `ApiKey` do not
appear anywhere in the diff.

### Finding: DTO field change from `string` to `decimal`/`long` — no string injection vector
- **Severity**: LOW
- **Confidence**: 96
- **File**: `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTickerDto.cs`
- **Category**: Security
- **Verdict**: PASS

The old DTO used string-encoded decimal fields (e.g. `"price": "67000.00"`). The new DTO uses
native `decimal` and `long` types matching the snapshot channel's numeric wire format. This is
strictly safer: the JSON deserializer enforces numeric type at parse time, eliminating any
opportunity for injection of unexpected string values (e.g. a very long string consuming
excessive memory during a string-to-decimal parse). No security regression; this is an improvement.

### Finding: `StreamKlineDto.Time` type corrected from `string` to `long`
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamKlineDto.cs:22-23`
- **Category**: Security
- **Verdict**: PASS

The `time` field in the kline frame arrives as a JSON number (unix nanoseconds), not a string.
The previous `string` type would silently produce `"0"` if the JSON value was numeric (the
deserializer would fail to bind a number to a string property without special handling, resulting
in the default). The corrected `long` type ensures the value is parsed correctly. The field is
documented as "not used by the decoder," so its security impact is limited; the change is correct.

---

## Opsec Check

No roadmap, competitive, monetization, or gateway information appears in commit messages, comments,
or test fixtures. All content is strictly technical. Confirmed clean.

---

## Summary

- PASS: JSON parsing safety — single parse, `JsonException` catch covers all try-body paths, no unbounded allocation.
- PASS: SSRF guard — `ValidateWsEndpoint` is unmodified; `wss://` + `*.kucoin.com` allowlist intact.
- PASS: Decimal overflow — `ChangeRate * 100m` safe for all realistic exchange values.
- PASS: Timestamp guard — `Datetime > 0` prevents zero/negative epoch misinterpretation; `FromUnixTimeMilliseconds` is safe for `long` inputs.
- PASS: CI change — `--filter 'Category!=Integration'` is a security improvement; removes live credential dependency from CI runners.
- PASS: No new attack surface — no new endpoints, credentials, signing paths, or external calls.
- PASS: DTO type change (`string` → `decimal`/`long`) — eliminates string injection vector; strictly safer.
- PASS: `StreamKlineDto.Time` corrected to `long` — correct type binding; decoder does not use the field.

## Final Verdict

APPROVED — All findings pass. The bugfix is a correct, minimal channel correction. No security
regressions. No credentials, signing paths, or SSRF guards were modified. The CI filter change
is a security improvement.
