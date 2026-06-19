# Code Review — TASK-028

## Verdict: APPROVED

## Score: 91/100

## Findings

### NOTE: Test coverage for happy-path is sparse — only 6 of 10 fields asserted (confidence: 75%)
`Apply_PopulatesAllFourExchanges_FromEnv` sets all 10 env vars but only asserts 6 of the 10 resulting properties (missing `BybitSecretKey`, `OkxApiKey`, `OkxSecretKey`, `BitgetApiKey`). If a future refactor mis-maps one of those 4 unasserted fields the test would still pass. This is a test-quality concern, not a correctness defect — `EnvCredentialBinder.Apply` reads each field directly from the provided delegate in a 1-to-1 mapping, so mis-mapping would only arise from a typo. The `Apply_LeavesNullsForUnsetVars` test also only spot-checks 2 of 10 properties, relying on the same argument. Neither omission exceeds the LR-005 threshold (zero coverage), so this is non-blocking.

**File**: `tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs:23-29`

**Fix**: Add assertions for the 4 remaining properties in the happy-path test. In the null test, assert all 10 or at minimum spot-check every exchange's passphrase/key variant.

### NOTE: Test project NoWarn lacks inline justification comments (confidence: 60%)
The review conventions state that `<NoWarn>` entries in `.csproj` files require a comment explaining each suppressed rule. The new test project at `CryptoExchanges.Net.Mcp.Tests.Unit.csproj:7` suppresses `CA1707;CA2007;CA1515;CS1591;xUnit1051` with no inline comment. However, the closest peer — `CryptoExchanges.Net.DependencyInjection.Tests.Unit.csproj` — carries the identical list with no comment either, so this follows the established pattern for test projects. Confidence is low that this represents a genuine defect given the consistent codebase pattern.

**File**: `tests/CryptoExchanges.Net.Mcp.Tests.Unit/CryptoExchanges.Net.Mcp.Tests.Unit.csproj:7`

**Fix**: If the mandate is strictly enforced, add a comment such as `<!-- CA1707: test method names use underscores; CA2007: tests don't need ConfigureAwait; CA1515: types public for cross-assembly access; CS1591: no XML docs in tests; xUnit1051: allowed constructor patterns -->` above the NoWarn line.

## Summary
TASK-028 delivers a clean scaffold. The build compiles with zero warnings under `TreatWarningsAsErrors=true` (verified via `dotnet build --configuration Release`). Both unit tests pass. The `EnvCredentialBinder.Apply` method correctly reads all 10 env vars and maps them to the corresponding `CryptoExchangesOptions` properties; null/empty env vars are handled gracefully because `Func<string, string?>` is allowed to return `null` and all option properties are `string?`. Guards on both reference-type parameters (`ArgumentNullException.ThrowIfNull(options)` and `ArgumentNullException.ThrowIfNull(getEnv)`) are present and correct — LR-001 applies to reference types here, not strings, so no `ThrowIfNullOrWhiteSpace` is needed. The CA1515 suppression is minimal (main project only, with a justification comment), correctly scoped, and necessary because `EnvCredentialBinder` must be `public` for the test assembly. The `Program.cs` correctly routes all logging to stderr via `LogToStandardErrorThreshold = LogLevel.Trace`, preserving stdout as the MCP transport channel. The `.WithStdioServerTransport().WithToolsFromAssembly()` chain is idiomatic for the MCP SDK. `TargetFramework`, `Nullable`, and `ImplicitUsings` are inherited from `Directory.Build.props` — their absence from the `.csproj` files is correct. The two findings above are both low-confidence notes; neither is blocking.
