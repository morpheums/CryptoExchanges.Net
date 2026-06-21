# Document Manifest

## Generated Documents

| Document | Status | Generated At | Approved |
|----------|--------|-------------|----------|
| PRD-FEAT-006 | generated | 2026-06-20 | pending |
| TRD-FEAT-006 | generated | 2026-06-20 | pending |
| ADR-002-streaming-async-endpoint-seam | generated | 2026-06-20 | pending |
| TEST-PLAN-FEAT-006 | generated | 2026-06-20 | pending |

## Classification

- **Type**: BROWNFIELD
- **Reasoning**: Existing 4-exchange library with verified exchange template. FEAT-006 clones
  that template for a 5th exchange. One targeted change to a shared abstraction (`IStreamProtocol`)
  is scoped and bounded. No greenfield architecture decisions needed.

## Existing Documentation Referenced

| Existing Document | Referenced By | How Used |
|-------------------|--------------|----------|
| `nazgul/context/objectives/FEAT-006-spec.md` | PRD, TRD, ADR-002, Test Plan | Primary spec — all sections |
| `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` | TRD, ADR-002 | Current seam contract; ADR-002 describes the change |
| `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs` | TRD, ADR-002 | Identifies call sites for `Endpoint` property (lines 286, 510) |
| `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` | TRD, ADR-002 | Binance implementation to migrate to new seam |
| `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs` | TRD | Reference pattern for base64 HMAC signing |
| `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs` | TRD | Reference pattern for passphrase-based mark-and-strip handler |
| `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs` | TRD | Confirms `Passphrase` already supported |
| `nazgul/archive/.../ADR-001-per-exchange-di-and-conventions.md` | ADR-002, TRD | Predecessor ADR; ADR-002 numbered sequentially after it |
| `nazgul/context/architecture-map.md` | TRD | Layer boundaries, data flow, dependency graph |
| `nazgul/context/test-strategy.md` | Test Plan | Testing framework, fixture patterns, project layout |
