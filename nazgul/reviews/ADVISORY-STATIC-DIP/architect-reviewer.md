# Architect Advisory — Static-to-Interface Design Assessment
**Type**: Advisory consultation (NOT a task gate — no diff, no TASK-ID)
**Requested by**: Maintainer (between M-OKX and M-BITGET)
**Date**: 2026-06-18
**Reviewer**: Architect Reviewer

---

## Context

This is a free-form architectural consultation requested directly by the maintainer between milestones. There is no active task, no diff.patch, and no code under review. The "do not edit anything" instruction in the original prompt referred to source code. This file is an administrative artifact only.

---

## Question

Which current `static` classes genuinely warrant an `interface + DI` seam, and which are fine as-is? Specific candidates: `ExchangeTimeSync`, `HmacSignature`/`SignatureEncoding`, `ExchangeServiceRegistration`, per-exchange `ServiceCollectionExtensions`, per-exchange `XxxSignatureService`, per-exchange `XxxClientComposer`.

---

## Decision Table

| Static Type | Location | Decision | Rationale |
|---|---|---|---|
| `ExchangeTimeSync` | `Core.Resilience` | MAKE INTERFACE + DI | Three (soon four) exchange clients call it on every `SyncServerTimeAsync`. The only realistic swap scenarios are test doubles and a future monotonic variant. Without an interface those require subclassing the exchange client. Cost is low; payoff is testability and openness. |
| `HmacSignature` + `SignatureEncoding` | `Core.Auth` | KEEP STATIC | Pure deterministic crypto primitive — equivalent to `SHA256.HashData`. No side effects, no I/O, no state. An `IHmacSignature` interface would buy nothing; callers would still use the exact same implementation every time. Keep static; separately fix Binance/Bybit to delegate to it instead of hand-rolling HMAC (mild duplication smell, not a DI problem). |
| `BinanceSignatureService`, `BybitSignatureService`, `OkxSignatureService` | Exchange `Auth/` | MAKE COMMON INTERFACE (not DI-registered) | All three are `internal sealed class` with a `Sign(string) : string` method. A shared `ISignatureService` in Core lets each `XxxSigningHandler` depend on an abstraction rather than the concrete, enabling handler-level mocking without spinning up a full exchange client. The real swap point is the interface on the service, not on the HMAC primitive. No DI registration needed — constructed in the composer/`ServiceCollectionExtensions` lambda. |
| `BinanceClientComposer`, `BybitClientComposer`, `OkxClientComposer` | Exchange `Internal/` | KEEP STATIC | DI extension glue / composition roots. Never injected; exist to wire concrete types together. Making them instance classes adds ceremony with no benefit. |
| `ExchangeServiceRegistration` | `Http/` | KEEP STATIC | Classic extension-method helper. Not consumed at runtime — only invoked at startup. No swappability need. |
| Per-exchange `ServiceCollectionExtensions` | Exchange root | KEEP STATIC | Standard .NET idiom for DI registration. |
| `ExchangeResiliencePipeline` | `Http/` | KEEP STATIC | Pure pipeline configuration called once at startup. No swap case. |
| `HttpClientPipelineBuilder` | `Http/` | KEEP STATIC | Factory-less composition helper. Variation points already injected as arguments. |

---

## Ruling on `ExchangeTimeSync`

**Verdict: make `IExchangeTimeSync` in Core, register as a single shared singleton.**

Note: TASK-REF-001 (already DONE, gate PASSED) moved `ExchangeTimeSync` into Core as a `public static class`. That satisfies the "single source of truth" goal. The remaining question is whether to add an `IExchangeTimeSync` interface on top of the already-landed static.

**Proposed interface shape:**

```csharp
// Core/Resilience/IExchangeTimeSync.cs
namespace CryptoExchanges.Net.Core.Resilience;

public interface IExchangeTimeSync
{
    long ComputeOffset(long serverTimeMs, long localNowMs);
    long ApplyOffset(long serverTimeMs, long localNowMs, long[] offsetHolder);
}
```

**Wiring cost:**
- `ExchangeTimeSync` implements `IExchangeTimeSync` (one-line change to the class declaration).
- Each `XxxExchangeClient` adds `IExchangeTimeSync timeSync` to its `internal` composition constructor.
- `XxxClientComposer.ComposeWith` gains a defaultable `IExchangeTimeSync? timeSync = null` parameter; when null, substitutes `new ExchangeTimeSync()` (or the static methods directly — since the static already exists, a thin adapter suffices).
- `ExchangeServiceRegistration.AddExchange` adds one line: `services.TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>()`.
- Each `ServiceCollectionExtensions.requestFinalizerFactory` lambda already has `IServiceProvider sp`; the `exchangeClientFactory` lambda resolves `sp.GetRequiredService<IExchangeTimeSync>()`.

**Registration shape:** single shared `TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>()`. The service is stateless (the offset lives in `_offsetHolder`, not in the service), so all exchange clients sharing one instance is safe.

**Files touched:** `ExchangeTimeSync.cs` (add interface implementation), `IExchangeTimeSync.cs` (new), the three `XxxExchangeClient.cs`, the three `XxxClientComposer.cs`, `ExchangeServiceRegistration.cs`, the three `ServiceCollectionExtensions.cs`. Ten files, all mechanical.

---

## Ruling on the HMAC / Signing Seam

**The right swap point is `ISignatureService` per exchange, not `HmacSignature` static.**

`HmacSignature.Compute` is the crypto primitive. There is no meaningful test scenario where you mock out the HMAC computation — you validate the output once and trust it. Making it injectable is the "mock out the filesystem" anti-pattern applied to cryptography.

The actual seam is between `XxxSigningHandler` and `XxxSignatureService`. Each handler takes its concrete service directly. A common `ISignatureService` interface:

```csharp
// Core/Auth/ISignatureService.cs
namespace CryptoExchanges.Net.Core.Auth;

public interface ISignatureService
{
    string Sign(string payload);
}
```

Each `XxxSignatureService` implements `ISignatureService`. Each `XxxSigningHandler` takes `ISignatureService signatureService` instead of the concrete type. This is a 3-file change per exchange (no DI registration needed — constructed inline in the composer lambda). Enables handler-level unit testing without constructing real HMAC state.

---

## Sequencing

**Do both on a dedicated pre-Bitget refactor task, before TASK-016.**

Bitget will add a fourth `SyncServerTimeAsync` caller and a fourth `XxxSignatureService`. If the seams exist before Bitget is scaffolded, the implementer follows the pattern for free. If not, the Bitget task either copies the anti-pattern a fourth time or tangles a structural change with a new feature.

`IExchangeTimeSync` and `ISignatureService` together touch ~10 files with mechanical changes, no public API break (all signature services and signing handlers are `internal`), and no behavior change. Low risk; high compounding value captured before the fourth exchange.

Suggested task ID: **TASK-REF-002** (or fold into TASK-016's wave as a pre-condition if the scope is tight).

---

## Summary

- KEEP STATIC: `HmacSignature`, `ExchangeServiceRegistration`, `ServiceCollectionExtensions` (per-exchange), `ExchangeResiliencePipeline`, `HttpClientPipelineBuilder`, `XxxClientComposer`
- MAKE INTERFACE + DI: `ExchangeTimeSync` → `IExchangeTimeSync` (single shared singleton in Core)
- MAKE INTERFACE (no DI): `XxxSignatureService` → `ISignatureService` per-exchange (constructor-injected into handler, constructed in composer)
- KEEP STATIC primitive: `HmacSignature` — fix Binance/Bybit to delegate to it as a separate cleanup (non-DI)

---

## Final Verdict

APPROVED

This advisory contains no blocking findings against existing code. All current static types in the codebase are used appropriately for their roles. The two recommended interface introductions (`IExchangeTimeSync`, `ISignatureService`) are pre-Bitget improvements, not regressions in the current code. No existing invariant is violated. The codebase as it stands today is sound.
