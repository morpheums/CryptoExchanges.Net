# API Review — TASK-029

## Verdict: APPROVED
Confidence: 97

## Summary
All three new types — `ToolResult<T>`/`ToolError`, `ToolInputs`, and `ToolRunner` — are pure additions in the `CryptoExchanges.Net.Mcp` project. No existing public interfaces, models, enums, or exceptions in Core, Http, Exchange, or DI are modified. The `public` accessibility is justified and correctly suppressed with `CA1515` because the separate test assembly requires direct reference. Naming conventions, method signatures, and XML doc coverage are all consistent with project patterns. Four findings are raised, all non-blocking.

## Findings

### public vs internal — CA1515 suppression is justified
- Severity: LOW
- Confidence: 90
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj, line 11
- Detail: The MCP project is an `OutputType=Exe` console host, not a library. Analyzer CA1515 ("Make public types internal") fires correctly for `ToolResult<T>`, `ToolError`, `ToolInputs`, and `ToolRunner`. The project suppresses CA1515 globally with a comment explaining the reason: the `CryptoExchanges.Net.Mcp.Tests.Unit` project is a separate assembly and cannot see `internal` types without `InternalsVisibleTo`. The suppression is legitimate — the alternative (`InternalsVisibleTo`) would be the weaker choice for a dotnet tool that is also published as a NuGet tool package, where `internal` across assemblies becomes noise. The approach is self-consistent.
- Fix: none required; the existing suppression comment in the csproj documents the rationale clearly
- Rule reference: none

### `FormatException` for `ParseSymbol` failures — contract is clear and correct
- Severity: LOW
- Confidence: 85
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolInputs.cs, lines 44–51
- Detail: `ParseSymbol` throws `FormatException` when the input is not `BASE/QUOTE` with valid assets. `ToolRunner.Categorize` maps `FormatException` to `"SymbolNotSupported"`. The coupling is a design-level contract: `ToolInputs` is the only code path that produces `FormatException` in an MCP tool body, and callers are expected to use `ParseSymbol` inside a `ToolRunner.RunAsync` lambda. This is workable but the category name `"SymbolNotSupported"` is not a perfectly precise description for all possible `FormatException` sources — if a third-party library used inside a tool threw `FormatException` for a different reason, it would be misclassified. In this codebase the risk is low because the only `FormatException`-throwing call site in the MCP layer is `ParseSymbol` itself. A domain-specific `SymbolFormatException : FormatException` would be cleaner, but is not required at preview stage.
- Fix: none required at this stage; if the MCP tool set grows significantly, consider introducing `SymbolFormatException : FormatException` to narrow the catch arm
- Rule reference: none

### `string?` contract on `TryParseExchange` / `TryParseInterval` — correct
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolInputs.cs, lines 31–36
- Detail: Both `TryParse*` methods accept `string?` and substitute `string.Empty` on null via `value ?? string.Empty`. This matches the .NET BCL convention (`int.TryParse(string? s, out int result)`) exactly and is the correct contract for agent-supplied inputs that may arrive as JSON null. The `Intervals` dictionary uses `StringComparer.Ordinal` (preserving `"1m"` vs `"1M"` case distinction) while `Exchanges` uses `StringComparer.OrdinalIgnoreCase` (tolerating mixed-case exchange names). Both choices are deliberate and correct.
- Fix: none required
- Rule reference: LR-001

### No breaking changes to existing public surface — confirmed
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: src/CryptoExchanges.Net.Core/ (all files), src/CryptoExchanges.Net.Binance/, src/CryptoExchanges.Net.Bybit/, src/CryptoExchanges.Net.Okx/, src/CryptoExchanges.Net.Bitget/, src/CryptoExchanges.Net.DependencyInjection/
- Detail: The implementation commit (73cc77e) touches exactly four files, all new: `ToolInputs.cs`, `ToolResult.cs`, `ToolRunner.cs`, `ToolPrimitivesTests.cs`. The simplify commit (4fdef79) touches `ToolInputs.cs` and `ToolRunner.cs` only. No Core interface, model, enum, or exception is added to or removed from. `IExchangeClient`, `IMarketDataService`, `ITradingService`, `IAccountService`, `IExchangeClientFactory`, `ISymbolMapper`, `PlaceOrderRequest`, all models, all enums, all exceptions, and all DI extension methods are unchanged.
- Fix: none required
- Rule reference: none

### XML doc coverage — adequate for app-tier types
- Severity: INFO
- Confidence: 95
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolResult.cs, ToolInputs.cs, ToolRunner.cs
- Detail: `GenerateDocumentationFile` is explicitly set to `false` in `CryptoExchanges.Net.Mcp.csproj` (line 6), overriding the `Directory.Build.props` default of `true`. All public members on all three types carry XML doc comments anyway: `ToolError`, `ToolResult<T>`, `Success`, `Failure`, `ToolInputs`, `TryParseExchange`, `TryParseInterval`, `ParseSymbol`, `ToolRunner`, `RunAsync` all have `<summary>` tags. Exception doc (`<exception cref="FormatException">`) is present on `ParseSymbol`. Coverage is complete and exceeds what is required for a non-library project.
- Fix: none required
- Rule reference: none

### NuGet package metadata — correctly set for dotnet tool
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj
- Detail: `<PackageId>`, `<Description>`, `<PackLicenseExpression>` (inherited via `Directory.Build.props`), `<PackAsTool>true</PackAsTool>`, and `<ToolCommandName>crypto-mcp</ToolCommandName>` are present. `<IsPackable>true</IsPackable>` is correct for a publishable dotnet tool. The test project correctly sets `<IsPackable>false</IsPackable>`. `<GenerateDocumentationFile>false</GenerateDocumentationFile>` in the Mcp project is an intentional override of the global default — this is valid for a tool host.
- Fix: none required
- Rule reference: none
