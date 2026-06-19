# Code Review — TASK-032

**Verdict**: APPROVED
**Confidence**: 97

## Summary

TASK-032 delivers a reflection-based roster guard test (3 facts, 44 total tests all passing), a package README, and a minor csproj addition to satisfy `PackageReadmeFile`. The tool method names in the README table exactly match the actual method names in `MarketDataTools.cs` and `AccountTools.cs`. The build is clean at zero warnings/errors under `TreatWarningsAsErrors=true`. No blocking issues found.

## Findings

### ToolMethods() uses correct attribute and binding flags — APPROVE (99%)

`BindingFlags.Public | BindingFlags.Static` is correct: all 12 `[McpServerTool]` methods in both tool classes are `public static`. The attribute type `McpServerToolAttribute` (imported from `ModelContextProtocol.Server`) matches what the tool files actually apply (`[McpServerTool]` is the shorthand for `McpServerToolAttribute`). The filter uses `.Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)`, so only genuinely decorated methods count. Adding a private method cannot inflate the count because `BindingFlags.Static` without `BindingFlags.NonPublic` excludes private members entirely. The guard is not gameable.

### Null-forgiving `!` in EveryTool_HasNonEmptyDescription — APPROVE (92%)

`m.GetCustomAttribute<DescriptionAttribute>()!.Description` — the `!` suppresses the nullable warning and will throw `NullReferenceException` (not an xUnit assertion failure) if a method carries `[McpServerTool]` but lacks `[Description]`. This is intentionally sharp behaviour for a guard test: the goal is to make missing `[Description]` fail loudly and unmistakably. All 12 current methods carry both attributes (verified in source). The behaviour is correct for a CI guard that must not silently pass.

### Write-verb banned list completeness — APPROVE (90%)

The banned set (`Place`, `Cancel`, `Create`, `Submit`, `Delete`) covers the standard trade-order write verbs for this domain. `Update`, `Modify`, `Amend`, `Send`, and `Execute` are absent from the ban list, but none of the current 12 tool names contain those words, and the spec does not list them. The set covers the spec's stated requirement. This is a low-risk gap that could be expanded in future if the roster grows.

### README tool table accuracy — APPROVE (99%)

Every tool name in the README table was cross-checked against `MarketDataTools.cs` and `AccountTools.cs`:

MarketDataTools: `GetPrice`, `GetTicker`, `GetOrderBook`, `GetKlines`, `GetRecentTrades`, `GetExchangeInfo` — all match.
AccountTools: `GetBalances`, `GetBalance`, `GetOpenOrders`, `GetOrder`, `GetOrderHistory`, `GetTradeHistory` — all match.

No discrepancies found.

### csproj PackagePath="\" syntax — APPROVE (97%)

`PackagePath="\"` places the file at the nupkg root, which is what the global `PackageReadmeFile>README.md</PackageReadmeFile>` (in `Directory.Build.props:17`) requires. Verified by actually packing the project: `README.md` appears at the archive root with no subdirectory prefix. The syntax is correct MSBuild.

### LR-001, LR-002, LR-003 applicability check — N/A

LR-001 (`ArgumentException.ThrowIfNullOrWhiteSpace`) — no new public string-parameter methods are introduced in this task (test file and documentation only). Not applicable.
LR-002, LR-003 — not applicable to this task.

## Rule References

None triggered. LR-001/LR-002/LR-003 explicitly checked and confirmed N/A for this task.
