# API Review — TASK-028

## Verdict: CHANGES_REQUESTED

## Score: 68/100

## Findings

### [REJECT] Missing PackageId and Description in Mcp csproj (confidence: 95%)
The CryptoExchanges.Net.Mcp.csproj does not declare PackageId or Description. Every other packable src/ project explicitly sets both: the Binance csproj sets PackageId=CryptoExchanges.Net.Binance and Description=Binance exchange implementation for CryptoExchanges.Net. Directory.Build.props provides Version, Authors, PackageLicenseExpression, and other shared metadata, but does NOT set PackageId or Description — those are project-specific and must be declared in each csproj. Without PackageId, the published NuGet tool package ID defaults to the assembly name and the package will have no description visible on nuget.org or via dotnet tool search.

Fix: Add to the PropertyGroup in src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj:
    PackageId: CryptoExchanges.Net.Mcp
    Description: Model Context Protocol (MCP) stdio server for CryptoExchanges.Net — exposes crypto exchange operations as MCP tools.

Pattern reference: src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj:5-6

---

### [REJECT] Microsoft.Extensions.Hosting version pinned to exact patch 10.0.9 instead of floating 10.0.* (confidence: 88%)
The Mcp csproj pins Microsoft.Extensions.Hosting at Version=10.0.9 (exact patch), but every other Microsoft.Extensions.* package across every exchange project uses Version=10.0.* (floating patch). Mixing exact-patch and wildcard in the same solution means the Mcp project will not automatically receive security or bug-fix patches that other projects pick up. ModelContextProtocol at Version=1.4.0 is appropriately exact-pinned as a third-party SDK. The hosting package should follow the established project-wide pattern.

Fix: Change the Microsoft.Extensions.Hosting PackageReference Version from 10.0.9 to 10.0.*

Pattern reference: src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj:23-25

---

### [CONCERN] CA1515 suppressed globally instead of at declaration site (confidence: 72%)
NoWarn CA1515 in the Mcp csproj silences the consider-making-internal diagnostic for all types in the assembly. Currently only EnvCredentialBinder requires public visibility for test-assembly access. If future tasks add tool classes, the analyzer will not flag any of them regardless of whether they actually need to be public. The trade-off is acknowledged (console apps cannot use InternalsVisibleTo for top-level-statement entry points), so this is non-blocking, and the comment on line 8 of the csproj documents the rationale.

---

### [CONCERN] Test project carries redundant explicit DependencyInjection ProjectReference (confidence: 65%)
CryptoExchanges.Net.Mcp.Tests.Unit.csproj adds a direct ProjectReference to CryptoExchanges.Net.DependencyInjection in addition to referencing CryptoExchanges.Net.Mcp. The Mcp project already references DI, so the test project inherits it transitively. The extra reference is harmless but adds maintenance surface and diverges from the convention of other test projects which reference only their subject assembly.

---

### [NOTE] LR-004 not applicable — Apply takes no array parameters (confidence: 99%)
EnvCredentialBinder.Apply(CryptoExchangesOptions options, Func getEnv) accepts no array parameter. Both arguments are guarded with ArgumentNullException.ThrowIfNull. LR-004 (null + minimum-length guard sequence for indexed array access) does not apply here.

---

### [NOTE] No breaking changes to existing public API (confidence: 99%)
The diff modifies only the .sln (additive wiring), nazgul/plan.md, and nazgul task frontmatter, plus newly created files. No existing interface, model, enum, or public type in src/CryptoExchanges.Net.Core/ or any exchange project is changed.

---

### [NOTE] GenerateDocumentationFile=false correctly overrides Directory.Build.props for a tool project (confidence: 99%)
The Mcp csproj explicitly overrides the inherited GenerateDocumentationFile=true from Directory.Build.props. This prevents CS1591 warnings under TreatWarningsAsErrors for an app/tool with no library public surface.

---

### [NOTE] IsPackable=false and IsTestProject=true correctly set in test project (confidence: 99%)
Both flags are set, consistent with all other test projects in the solution.

---

### [NOTE] Tool command name crypto-mcp is acceptable (confidence: 85%)
crypto-mcp is short, descriptive, kebab-cased, and does not collide with known dotnet tool names.

---

### [NOTE] Program.cs stderr redirect is correct for MCP stdio transport (confidence: 95%)
builder.Logging.AddConsole with LogToStandardErrorThreshold=LogLevel.Trace routes all log levels to stderr, keeping stdout clean for the MCP JSON-RPC framing. This is the correct pattern for stdio-mode MCP servers.

## Summary

TASK-028 delivers a clean console/tool scaffold with correct stdio logging, proper null-guarding in EnvCredentialBinder.Apply, and adequate unit test coverage for the binder. Two blocking issues require fixes: (1) CryptoExchanges.Net.Mcp.csproj is missing PackageId and Description, which are project-specific metadata not inherited from Directory.Build.props and are required by the established NuGet convention across all src/ packages; (2) Microsoft.Extensions.Hosting is pinned to exact patch version 10.0.9 rather than the floating 10.0.* pattern used consistently for all Microsoft.Extensions.* packages in the solution. Both fixes are one-line changes in a single file. No existing public API surface was modified; no breaking changes detected.
