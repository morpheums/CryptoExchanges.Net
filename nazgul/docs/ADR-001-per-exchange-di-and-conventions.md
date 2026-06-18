# ADR-001: Per-exchange DI registration + cross-cutting conventions

- **Status**: Accepted
- **Date**: 2026-06-18
- **Context**: M2 exchange expansion (Binance shipped; Bybit added in PR #11; OKX + Bitget queued). Decision raised by the maintainer reviewing `ServiceCollectionExtensions.cs`; assessed by the architect-reviewer.

## Decision 1 — DI registration lives in each exchange's own assembly

Each exchange assembly (`CryptoExchanges.Net.Bybit`, `.Binance`, `.Okx`, `.Bitget`) ships its **own** `AddXxxExchange(this IServiceCollection, Action<XxxOptions>?)` extension. The aggregation package `CryptoExchanges.Net.DependencyInjection` keeps only a thin `AddCryptoExchanges` convenience that delegates to those per-exchange extensions.

**Why.** The previous design centralized all `AddXxxExchange` methods in the DI package, which therefore compile-time-referenced *every* exchange assembly. For a NuGet SDK this forces a consumer who wants one exchange to transitively pull in all of them (binary/dependency bloat) — a genuine correctness issue, not just style. It also violates OCP (the shared file + `CryptoExchangesOptions` must be edited per exchange) and DRY (~90% duplication, compounding per exchange).

**Consequences.**
- Each exchange assembly takes a dependency on `Microsoft.Extensions.DependencyInjection.Abstractions` + `Microsoft.Extensions.Http` (previously only the DI package needed them). Acceptable.
- Consumers who want selective loading reference the exchange assembly directly; `AddCryptoExchanges` remains for "I want all of them."
- **Breaking change** (namespace/package move of `AddBinanceExchange`/`AddBybitExchange`). Acceptable pre-v1.0 (currently 0.1.0). Optionally leave `[Obsolete("Use CryptoExchanges.Net.<Exchange>")]` forwarders in the DI package for one minor version. `AddCryptoExchanges` callers need no change.

**Rejected alternatives.** (c) reflection/registry discovery — over-engineered + not AOT-safe at this scale; static registry would introduce prohibited global mutable state. (d) a generic `AddExchange<TOptions>` helper alone — only removes ~40% of the duplication and the type-specific variation (per-exchange HttpClient headers, `ComposeForDi` concrete types) resists generic extraction; best used as a *companion* to Decision 1, not a substitute.

**Rollout.** Apply at the start of the M-OKX milestone, folded with TASK-009 (the signing/credential generalization that already touches shared code). Move Binance + Bybit registrations into their assemblies and implement OKX/Bitget DI in-assembly from day one — cheaper now (2 methods) than after 4 exchanges exist.

## Decision 2 — Why this wasn't caught earlier (process)

The pattern was inherited from Binance (pre-expansion); Rule 3 ("follow existing patterns exactly") told each Bybit task to clone it; and the review gate is a **task-scoped conformance review**, not a standing architecture review. The architect's codified layering invariant even *permits* `DI → Exchange(s)`. So faithful clones passed. To close this gap:
- architect-reviewer gains invariant #10 (package-level coupling / consumer cost), a "question the reference pattern, not just conformance" mandate (raise pattern smells as non-blocking CONCERNs), and a **milestone-boundary macro-architecture pass** distinct from per-task review.

## Conventions (apply by default; reviewers enforce)

1. **Per-exchange DI** — see Decision 1.
2. **Internal by default** — only `XxxExchangeClient` + `XxxOptions` are public per exchange; signing, services, HTTP wrappers, composer, DTOs are `internal` (`InternalsVisibleTo` for that exchange's test + DI only). Binance's signing types remain `public` for legacy reasons — harmonize to internal during TASK-009.
3. **Guard `JsonElement.ValueKind` before typed accessors** — `GetString()`/`GetInt32()` throw `InvalidOperationException` (not `JsonException`); an unguarded read escapes a `catch (JsonException)` and can crash the pipeline. (Found in `BybitErrorTranslator` by an external PR bot; fixed in PR #11.)
4. **`ThrowIfNullOrWhiteSpace` / `ThrowIfNull` at every public/internal boundary** — including static helpers and HTTP-client `endpoint` params.
5. **Clamp, don't throw, on interface-default parameters** — when an exchange cap is below an interface's default arg (e.g. `limit = 500` vs cap 50), clamp (`Math.Min`) rather than throw, so default-path callers don't get an LSP-violating exception.
6. **DeltaMapper for all DTO→model mapping**; `ISymbolMapper` stays bespoke (existing mandate).
