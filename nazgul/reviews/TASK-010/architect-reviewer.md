# Architect Review — TASK-010: OKX Project Scaffold + Passphrase Options + DI Seam Stub

**Reviewer**: Architect Reviewer (claude-sonnet-4-6)
**Date**: 2026-06-18
**Branch**: feat/m3-okx
**Commit under review**: af642795acfca20118a0f73c8a87c0de7c615b73

---

## Review Checklist

- [x] Core has no knowledge of any exchange (no new refs in Core) — not applicable to this diff; no Core edits
- [x] Http has no knowledge of any exchange — not applicable; no Http edits
- [x] Exchange client internals stay internal — OkxOptions is the only public type; correct for scaffold stage
- [x] No new behavior added to existing public interfaces — no interface changes
- [x] csproj ProjectReferences: Core + Http ONLY (verified via `dotnet list src/CryptoExchanges.Net.Okx reference`)
- [x] No IVT to DependencyInjection package (ADR-001 compliant)
- [x] ExchangeId.Okx reused from Core/Enums/Enums.cs:134 — no Core enum edit
- [x] PackageReferences mirror Bybit exactly (DeltaMapper 1.2.0, DI.Abstractions 10.0.*, Http 10.0.*, Options 10.0.*)
- [x] NoWarn list identical to Bybit (CA1812;CA1056;CA1031;CA2000;CA1305;CA1032;CS1591)
- [x] IVT entries: Tests.Unit, Tests.Integration, DynamicProxyGenAssembly2 — matches Bybit's current set exactly
- [x] GlobalUsings identical to Bybit (System.Text.Json + Serialization, Core.Enums, Core.Interfaces, Core.Models); Auth omitted from globals (referenced explicitly in OkxOptions.cs only)
- [x] Build: 0 Warnings, 0 Errors (verified)
- [x] SLN: project entry GUID {179D89FC...} consistent with config-section entries; Bybit GUID {D5E6F7A8...} reorder is cosmetic (all 13 occurrences present); OKX nested under src solution folder {827E0CD3...}
- [x] No global state, no static mutable fields
- [x] No new exchange references in shared/aggregation packages

---

## Findings

### Finding: ToCredentials() doc claims passphrase is always required, but ExchangeCredentials accepts null
- **Severity**: LOW
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:35-37`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: The XML `<exception>` doc on `ToCredentials()` states that `Passphrase` being empty/whitespace throws. This is accurate — `ExchangeCredentials(apiKey, secretKey, passphrase)` validates a non-null passphrase as non-whitespace. However, `ExchangeCredentials` accepts `null` as a valid passphrase (for exchanges that don't need one). `OkxOptions.Passphrase` defaults to `string.Empty`, so calling `ToCredentials()` with an unset Passphrase will throw (empty string is not null, and a non-null passphrase must be non-whitespace). This is correct OKX behavior for signed endpoints, but it also means `ToCredentials()` is not safe for the public-data-only use case the doc describes ("Leave empty when only public market-data endpoints are used"). A caller who only wants public data and never calls `ToCredentials()` is fine, but the discrepancy between the property-level doc ("leave empty for public data") and the method-level behavior (throws on empty) could confuse consumers.
- **Fix**: Either (a) change the property default to `null` (requires `string?` type, which aligns with `ExchangeCredentials.Passphrase` being `string?`), or (b) add a guard in `ToCredentials()` that passes `string.IsNullOrWhiteSpace(Passphrase) ? null : Passphrase` — allowing a null credential when no passphrase is set, consistent with what `ExchangeCredentials` was designed to support. Option (b) is least invasive for this scaffold.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:40` — `passphrase = null` is the no-passphrase sentinel

### Finding: SLN config-section reorder of Bybit entries
- **Severity**: LOW
- **Confidence**: 95
- **File**: `CryptoExchanges.Net.sln` (diff lines 37-48 removed, reinserted at 18-29)
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: The diff moves the Bybit `{D5E6F7A8...}` config block earlier in the GlobalSection. This is purely cosmetic (MSBuild is order-independent for config sections); confirmed that all 13 Bybit GUID occurrences remain. The build succeeds. No data loss.
- **Fix**: None required.
- **Pattern reference**: N/A (cosmetic)

---

## Summary

- PASS: Layer chain preserved — `dotnet list src/CryptoExchanges.Net.Okx reference` confirms Core + Http only; no Binance/Bybit/DI refs.
- PASS: IVT posture correct per ADR-001 — no DependencyInjection IVT; three correct entries (Tests.Unit, Tests.Integration, DynamicProxyGenAssembly2).
- PASS: PackageReferences mirror Bybit exactly (DeltaMapper 1.2.0, DI.Abstractions 10.0.*, Http 10.0.*, Options 10.0.*).
- PASS: NoWarn list identical to Bybit.
- PASS: GlobalUsings identical to Bybit; Auth namespace referenced explicitly only in OkxOptions.cs.
- PASS: ExchangeId.Okx reused from Core (line 134); no Core enum edit.
- PASS: OkxOptions is public sealed, all members XML-documented; no previously-internal type exposed.
- PASS: No new behavior on existing interfaces (IMarketDataService, ITradingService, IAccountService, IExchangeClient).
- PASS: Build succeeds with 0 Warnings, 0 Errors under TreatWarningsAsErrors.
- PASS: SLN registration correct — unique GUID, full config entries, nested under src solution folder.
- PASS: No global state, no static mutable fields introduced.
- PASS: No shared/aggregation package compile-time references a new exchange (ADR-001 Invariant 10).
- CONCERN: ToCredentials() behavior mismatch — empty Passphrase throws but property doc says "leave empty for public data." Non-blocking; the method is only called when signing is wired up (future tasks), but should be resolved before TASK-011 consumes it. (confidence: 75/100, non-blocking)

---

## Final Verdict

**APPROVED** — confidence: 96/100

All hard architectural invariants pass. The one CONCERN (ToCredentials/empty-passphrase mismatch) is non-blocking at scaffold stage since the method is intentionally forward-looking and no signing code calls it yet. It should be resolved in TASK-011 when signing is wired up.
