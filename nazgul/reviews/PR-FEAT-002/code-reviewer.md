# Code Review — PR-FEAT-002 (diff audit: feat/FEAT-002-mcp-server-readonly)

**Reviewer**: code-reviewer
**Scope**: PR-level diff audit of the full FEAT-002 branch against 5 house rules
**Diff**: full branch diff (CHANGELOG.md, sln, Directory.Build.props, README.md, src/CryptoExchanges.Net.Mcp/**, tests/CryptoExchanges.Net.Mcp.Tests.Unit/**)
**Date**: 2026-06-19

---

## Final Verdict

CHANGES_REQUESTED

---

## Violations

### Violation 1 — Rule 1: Two top-level types in one file

- **File**: `src/CryptoExchanges.Net.Mcp/ToolResult.cs` (diff line ~33281)
- **Rule**: Rule 1 — One type per file
- **Description**: `ToolResult.cs` defines two distinct top-level types: `ToolError` (a `sealed record`) and `ToolResult<T>` (a `sealed record`). The project convention is one top-level type per file, named after the type.
- **Fix**: Extract `ToolError` to its own file `src/CryptoExchanges.Net.Mcp/ToolError.cs`.

---

### Violation 2 — Rule 4(b): Restate-the-code comments in AccountTools.cs

- **File**: `src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs` (diff lines ~33447 and ~33457)
- **Rule**: Rule 4 — Comment conventions (no comments that just restate the code)
- **Description**:
  - `// Shared path for tools that only need exchange resolution (no symbol parsing).` — the method name `Run<T>`, its parameter list (no `symbol` param), and the lambda signature `Func<IExchangeClient, CancellationToken, Task<T>>` already communicate this completely. The comment restates what the code already says.
  - `// Shared path for tools that require symbol parsing.` — similarly, the method name `Resolve<T>` and its `string symbol` parameter already express this.
- **Fix**: Remove both inline comments. The methods are self-documenting.

---

### Violation 3 — Rule 4(b): Restate-the-code comment in MarketDataTools.cs

- **File**: `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs` (diff line ~33594)
- **Rule**: Rule 4 — Comment conventions (no comments that just restate the code)
- **Description**: `// Shared path for the symbol-required tools.` on the private `Resolve<T>` method restates what the method name and `string symbol` parameter already convey.
- **Fix**: Remove the comment.

---

## Passing Checks

- **Rule 2** (Wire DTOs internal, in Dtos/): No wire DTOs introduced. The MCP layer operates only on `Core.Models` types returned by `IExchangeClient`. PASS.
- **Rule 3** (DTO naming): No wire DTOs introduced. PASS.
- **Rule 5** (DeltaMapper for DTO→model mapping): No DTO→model mapping code exists in the MCP project. It is a pure facade over `IExchangeClient`. PASS.
- **Rule 1** (all other files): All other new files define exactly one top-level type. `ToolInputs.cs` → `ToolInputs`, `ToolRunner.cs` → `ToolRunner`, `EnvCredentialBinder.cs` → `EnvCredentialBinder`, `AccountTools.cs` → `AccountTools`, `MarketDataTools.cs` → `MarketDataTools`. PASS.
- **Rule 4** (interface XML docs): `ToolInputs`, `ToolRunner`, `EnvCredentialBinder`, `AccountTools`, `MarketDataTools` are not implementations of any interface — they are standalone static types carrying their own `<summary>` docs. The `ToolRunner.Categorize` private method comment (ordering rationale for switch arms) is a legitimate non-obvious "why" and should be kept. PASS.

---

## Summary

| # | Rule | File | Issue | Verdict |
|---|------|------|-------|---------|
| 1 | Rule 1 | `src/CryptoExchanges.Net.Mcp/ToolResult.cs` | Two top-level types (`ToolError` + `ToolResult<T>`) in one file | REJECT |
| 2 | Rule 4(b) | `src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs` | Two restate-the-code comments on private helpers `Run<T>` and `Resolve<T>` | REJECT |
| 3 | Rule 4(b) | `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs` | One restate-the-code comment on private helper `Resolve<T>` | REJECT |
