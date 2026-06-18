# Code Review: TASK-001

**Verdict**: APPROVED
**Reviewer**: code-reviewer
**Confidence**: 72

## Summary
The scaffold is structurally correct — csproj mirrors Binance exactly, GlobalUsings correctly omits Services/Auth namespaces that do not exist yet, and the build is verified clean at 0 warnings/0 errors. Two non-blocking concerns are noted (both confidence < 80 and both inherited from the Binance reference pattern rather than newly introduced here); neither constitutes a blocking defect for a pure scaffolding task.

## Findings

### Finding 1: ReceiveWindow typed `decimal` — Bybit API contract requires integer milliseconds
- **Severity**: MEDIUM
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs:21`
- **Description**: `ReceiveWindow` is declared `decimal ReceiveWindow { get; set; } = 5000m`. The Bybit V5 REST API requires `recvWindow` as an integer (long/int) in the query string. Sending a `decimal` value (e.g. `5000.0`) will either fail serialisation or be rejected by the exchange. This is a latent bug that will surface when the HTTP layer serialises this field. The exact same issue exists in `BinanceOptions:27`, making this a copy-fidelity problem rather than a new defect introduced here; confidence stays below 80 because no serialisation code exists yet in this scaffolding wave.
- **Verdict**: CONCERN (confidence 72, non-blocking — should be addressed before TASK-002 ships the signing layer)

### Finding 2: XML doc summary contains "(decimal)" noise in ReceiveWindow description
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs:20`
- **Description**: The `<summary>` reads `"Receive window in milliseconds (decimal). Default: 5000."` — the parenthetical `(decimal)` leaks the implementation type into the user-facing doc comment. For BybitOptions the text is also factually incorrect guidance since Bybit expects an integer value (see Finding 1). Cloned faithfully from `BinanceOptions:26`.
- **Fix**: Remove `(decimal)` from the summary. Correct text: `"Receive window in milliseconds. Default: 5000."` Apply the same fix to `BinanceOptions` in a paired cleanup.
- **Verdict**: CONCERN (confidence 95, non-blocking — cosmetic/docs issue)

## Checklist
- XML docs on all public members: PASS — `BybitOptions` class and all five properties carry `/// <summary>` comments; CS1591 is suppressed in NoWarn as expected but the comments are present anyway.
- BinanceOptions mirror (fields): PASS — BaseUrl, ApiKey, SecretKey, TimeoutSeconds, ReceiveWindow all present with identical types and defaults. The `decimal` ReceiveWindow is a faithful mirror of BinanceOptions (tracked in Finding 1).
- sealed class: PASS — `public sealed class BybitOptions` matches the pattern.
- C# 13 conventions: PASS — file-scoped namespace, settable properties with default values, no extraneous syntax.
- No stubs: PASS — pure scaffolding task (options + project files only); no stub methods or empty implementations present.
- csproj mirrors Binance: PASS — identical ProjectReferences (Core + Http), identical PackageReferences (DeltaMapper 1.2.0, ME.Http 10.0.*, ME.Options 10.0.*), identical NoWarn list, InternalsVisibleTo entries updated correctly for Bybit assembly names.
- GlobalUsings omits non-existent namespaces: PASS — Services and Auth usings correctly absent; Binance GlobalUsings includes `CryptoExchanges.Net.Binance.Services` and `CryptoExchanges.Net.Binance.Auth` which do not exist in Bybit yet.
- Layer isolation (no DI/cross-exchange ref): PASS — csproj references Core + Http only.
- Build clean: PASS — implementer-verified 0 warnings / 0 errors under TreatWarningsAsErrors.

## Final Verdict
APPROVED
