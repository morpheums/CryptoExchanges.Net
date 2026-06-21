# Document Manifest

## Generated Documents

| Document | Status | Generated At | Approved |
|----------|--------|-------------|----------|
| PRD-FEAT-006 | generated | 2026-06-20 | pending |
| TRD-FEAT-006 | generated | 2026-06-20 | pending |
| ADR-002-streaming-async-endpoint-seam | generated | 2026-06-20 | pending |
| TEST-PLAN-FEAT-006 | generated | 2026-06-20 | pending |
| PRD-FEAT-007 | generated | 2026-06-21 | pending |
| TRD-FEAT-007 | generated | 2026-06-21 | pending |
| ADR-003-root-packageid-for-all-exchanges-meta-bundle | generated | 2026-06-21 | pending |
| TEST-PLAN-FEAT-007 | generated | 2026-06-21 | pending |

## Classification

- **Type**: BROWNFIELD
- **Reasoning**: Existing 4-exchange library with verified exchange template. FEAT-006 clones
  that template for a 5th exchange. One targeted change to a shared abstraction (`IStreamProtocol`)
  is scoped and bounded. No greenfield architecture decisions needed.
  FEAT-007 is a brownfield refactor: package identity rename + namespace move + test decoupling,
  no runtime behavior change.

## Existing Documentation Referenced

| Existing Document | Referenced By | How Used |
|-------------------|--------------|----------|
| `nazgul/context/objectives/FEAT-006-spec.md` | PRD-FEAT-006, TRD-FEAT-006, ADR-002, TEST-PLAN-FEAT-006 | Primary spec — all sections |
| `nazgul/context/objectives/FEAT-007-spec.md` | PRD-FEAT-007, TRD-FEAT-007, ADR-003, TEST-PLAN-FEAT-007 | Primary spec — all sections |
| `docs/superpowers/specs/2026-06-21-rename-di-aggregator-root-metapackage-design.md` | PRD-FEAT-007, TRD-FEAT-007, ADR-003, TEST-PLAN-FEAT-007 | Approved brainstorming design — locked decisions, scope, acceptance criteria, build sequence |
| `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` | TRD-FEAT-006, ADR-002 | Current seam contract; ADR-002 describes the change |
| `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs` | TRD-FEAT-006, ADR-002 | Identifies call sites for `Endpoint` property (lines 286, 510) |
| `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` | TRD-FEAT-006, ADR-002 | Binance implementation to migrate to new seam |
| `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` | TRD-FEAT-007, ADR-003 | Current aggregator implementation; namespace declaration is the only source change |
| `src/CryptoExchanges.Net.DependencyInjection/CryptoExchangesOptions.cs` | TRD-FEAT-007, ADR-003 | Current options type; namespace declaration is the only source change |
| `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/DiRegistrationTests.cs` | TRD-FEAT-007, TEST-PLAN-FEAT-007 | Existing tests preserved with namespace/using update |
| `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/ExchangeClientFactoryTests.cs` | TRD-FEAT-007, TEST-PLAN-FEAT-007 | Existing tests preserved with namespace/using update |
| `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs` | TEST-PLAN-FEAT-007 | Identifies the 3 AddCryptoExchanges tests to be removed from this project |
| `tests/CryptoExchanges.Net.{Bybit,Okx,Bitget}.Tests.Unit/*.csproj` | TRD-FEAT-007, TEST-PLAN-FEAT-007 | Four test projects being decoupled; each holds ProjectReference to aggregator |
| `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs` | TRD-FEAT-006 | Reference pattern for base64 HMAC signing |
| `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs` | TRD-FEAT-006 | Reference pattern for passphrase-based mark-and-strip handler |
| `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs` | TRD-FEAT-006 | Confirms `Passphrase` already supported |
| `nazgul/archive/.../ADR-001-per-exchange-di-and-conventions.md` | ADR-002, ADR-003, TRD-FEAT-006, TRD-FEAT-007 | Predecessor ADR; ADRs numbered sequentially after it |
| `nazgul/context/architecture-map.md` | TRD-FEAT-006, TRD-FEAT-007 | Layer boundaries, data flow, dependency graph |
| `nazgul/context/test-strategy.md` | TEST-PLAN-FEAT-006, TEST-PLAN-FEAT-007 | Testing framework, fixture patterns, project layout |
