# Architect Review — TASK-032

**Verdict**: APPROVED
**Confidence**: 97

## Summary

TASK-032 delivers a reflection-based roster guard test, a package-level README, and a root README section for the MCP server. All three deliverables are structurally sound. The build compiles cleanly with `TreatWarningsAsErrors=true` and all 44 MCP unit tests pass. No architectural invariants are violated.

## Findings

### Roster guard reflection approach is correct and complete — APPROVE (98%)

`ToolRosterTests.ToolMethods()` queries `BindingFlags.Public | BindingFlags.Static` on exactly the two tool classes (`MarketDataTools`, `AccountTools`) then filters by `McpServerToolAttribute`. This precisely mirrors how the MCP SDK itself discovers tools — both classes carry `[McpServerToolType]` and every tool method is `public static` annotated with `[McpServerTool]`. The three invariants enforced are:

1. Count == 12 (any addition or deletion breaks this immediately).
2. Every method carries a non-empty `[Description]` (tested via `DescriptionAttribute`, which is how the `[Description]` attribute is stored at runtime).
3. No method name contains a write verb (Place, Cancel, Create, Submit, Delete) — case-insensitive.

The guard is genuinely protective: a future developer adding a write tool, or a tool without a description, or forgetting to annotate with `[McpServerTool]`, will hit a failing test before the PR can land. The binding-flags choice is not accidentally permissive — MCP requires static public methods, so non-public or instance methods would not be discovered by the runtime either. This is the correct, tight guard.

### csproj README pack fix is minimal and correct — APPROVE (99%)

The single added `<ItemGroup>` with `<None Include="README.md" Pack="true" PackagePath="\" />` is the standard NuGet pattern for packing a readme when `<PackageReadmeFile>` is set at the MSBuild props level. `PackagePath="\"` places the file at the package root, which is where NuGet expects it for `PackageReadmeFile`. No extraneous changes; no dep-direction change. The csproj already referenced all four exchanges and the DI package — this diff introduces no new ProjectReferences.

### Root README MCP section is accurate — APPROVE (95%)

The new section correctly states "read-only", names all four exchanges, gives the exact count (12 tools, 6 + 6), and correctly distinguishes credential-free market tools from credentialed account tools. The link to `src/CryptoExchanges.Net.Mcp/README.md` is a valid relative path from the repo root.

One minor stale-data note (non-blocking, pre-existing): the Roadmap checklist in the root README still shows `- [ ] Bybit implementation` and `- [ ] MCP server wrapper` as unchecked even though both shipped in M2. This is a pre-existing README maintenance gap, not introduced by this diff (the diff only adds the new MCP section above the Building heading). Flagging as a CONCERN for awareness.

### Package README content correctly represents the architecture — APPROVE (97%)

The MCP README accurately documents: install command, tool-command name (`crypto-mcp` matching `<ToolCommandName>` in the csproj), all ten env vars across four exchanges, the 12-tool table split 6/6, symbol format, error categories, and exchange identifiers. The read-only invariant is stated prominently at the top. No misleading claims found.

### Stale roadmap entries — CONCERN (70%)

The root README roadmap still lists `- [ ] Bybit implementation` and `- [ ] MCP server wrapper` as uncompleted. Both shipped in M2 (Bybit in M-BYBIT, MCP across TASK-025 through TASK-032). This is a pre-existing stale state not introduced by this diff, so it is non-blocking. A follow-up commit to tick these items `[x]` and update the directory tree in the README (which still shows `CryptoExchanges.Net.Bybit/ # [planned]`) would clean up the public-facing record.

## Rule References

None (LR-001 through LR-003 do not apply to this test + docs + packaging task).
