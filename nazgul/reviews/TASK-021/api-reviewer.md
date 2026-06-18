# API Review: TASK-021 — BitgetHttpClient + IBitgetHttpClient

**Reviewer**: api-reviewer
**Date**: 2026-06-18
**VERDICT: APPROVED**

---

## Scope

Internal surface only — `IBitgetHttpClient` and `BitgetHttpClient`. Not public NuGet API. Judged as internal contract consistency, pattern fidelity, and InternalsVisibleTo correctness.

---

## Findings

### Finding: Interface is byte-for-byte identical to IOkxHttpClient
- **Severity**: LOW
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs:1-20`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A — this is the desired state.
- **Detail**: All four members (`GetAsync<T>`, `PostAsync<T>(Dictionary)`, `PostAsync<T>(object)`, `DeleteAsync<T>`) match `IOkxHttpClient` (`src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs:1-20`) exactly in name, generics, parameter order, parameter types, nullable annotations, and default values (`signed = false` for GET, `signed = true` for POST/DELETE, `ct = default` last). A developer familiar with the OKX client finds this immediately familiar.

### Finding: Overload resolution between PostAsync(Dictionary) and PostAsync(object)
- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs:10,16`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `Dictionary<string, string>` is a reference type and derives from `object`, so a caller passing `null` as the second argument could resolve ambiguously. However, this is an internal interface with controlled callers (service classes in the same assembly) — there is no ambiguity risk in practice. The calling pattern is either explicit `Dictionary` literal / variable, or a typed object (e.g. list/record), never a bare `null`. Pattern matches OKX exactly where this was already accepted.
- **Fix**: No action required. (If this were public API, the `object` overload would warrant a named factory or distinct method name — but internal + controlled callers = acceptable.)

### Finding: XML doc coverage on interface; inheritdoc on impl
- **Severity**: LOW
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs:3-19`, `BitgetHttpClient.cs:39,51,63,84,90`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A. Interface carries `<summary>` on the type (`line 3`) and on every member (`lines 6, 9, 12-15, 18`). The `PostAsync<T>(object)` overload has a multi-line `<summary>` with rationale. `BitgetHttpClient` uses `/// <inheritdoc />` on every public method (`lines 39, 51, 63, 84, 90`). Matches the project convention documented in the comment-and-interface-conventions memory.

### Finding: Signed defaults consistent with OKX
- **Severity**: LOW
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs:7,10,16,19`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A. `GetAsync` defaults `signed = false` (correct — public market data); `PostAsync` and `DeleteAsync` default `signed = true` (correct — order and account mutations). Matches `IOkxHttpClient` defaults.

### Finding: InternalsVisibleTo — acceptance criterion 3
- **Severity**: HIGH
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bitget/CryptoExchanges.Net.Bitget.csproj:19-23`
- **Category**: NuGet Conventions / InternalsVisibleTo
- **Verdict**: PASS
- **Issue**: N/A. `CryptoExchanges.Net.Bitget.Tests.Integration` is present (`line 21`), satisfying acceptance criterion 3. `CryptoExchanges.Net.Bitget.Tests.Unit` is also present (`line 19`). `DynamicProxyGenAssembly2` for NSubstitute mocking is present (`line 22`). No consumer application project is granted visibility — encapsulation intact. Pattern matches `CryptoExchanges.Net.Okx.csproj:19-23` exactly (including comment style).

### Finding: CancellationToken as last parameter on all methods
- **Severity**: LOW
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs:7,10,16,19`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A. `CancellationToken ct = default` is the last parameter on all four interface members, matching the project-wide convention (`IExchangeClient.cs:18`) and the OKX counterpart.

### Finding: ConfigureAwait(false) applied to all async paths
- **Severity**: LOW
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:47-48, 60, 72, 80-81, 86-87, 92-93`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A. Every `await` in `BitgetHttpClient` uses `.ConfigureAwait(false)` — matches OKX implementation exactly.

### Finding: ArgumentException guards on all public entry points
- **Severity**: LOW
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:44, 56, 67-68, 89`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A. `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` guards all four public methods. The object-body `PostAsync` also adds `ArgumentNullException.ThrowIfNull(body)`. Matches OKX pattern.

### Finding: Private PostJsonAsync helper not on interface
- **Severity**: LOW
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:75-82`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: N/A. The `private` helper is correctly absent from the interface. Both `PostAsync` overloads delegate to it — correct deduplication of the `StringContent`/`HttpRequestMessage` construction. Pattern identical to OKX.

### Finding: OkxHttpClient has an extra inline comment inside PostJsonAsync; BitgetHttpClient omits it
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs:75-82` vs `OkxHttpClient.cs:74-83`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `OkxHttpClient.PostJsonAsync` repeats the signing comment at `line 77`. `BitgetHttpClient.PostJsonAsync` omits the duplicate comment — the identical rationale is already stated in `PostAsync<T>(Dictionary)` at `line 57-58`. The omission is a lean-comment improvement, not a regression. No action required.

---

## Summary

- PASS: Interface shape — byte-identical to `IOkxHttpClient`; method names, generics, parameter types, nullable annotations, defaults all match.
- PASS: Signed defaults — GET false, POST/DELETE true (consistent with OKX).
- PASS: XML docs on interface, `<inheritdoc />` on impl — matches project convention.
- PASS: `CancellationToken ct = default` last on all methods — consistent with project-wide convention.
- PASS: `ConfigureAwait(false)` on every await — consistent with OKX.
- PASS: Argument guards — `ThrowIfNullOrWhiteSpace` + `ThrowIfNull` where appropriate.
- PASS: `InternalsVisibleTo` covers Integration + Unit test assemblies and `DynamicProxyGenAssembly2`; no consumer apps.
- PASS: Overload resolution (Dictionary vs object) — internal controlled callers, no ambiguity risk; matches accepted OKX pattern.
- PASS: `private PostJsonAsync` helper correctly absent from interface.

**No blocking issues found.**

## Final Verdict

APPROVED
