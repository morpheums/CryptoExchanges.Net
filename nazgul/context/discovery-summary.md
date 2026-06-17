# Discovery Summary

## Run Metadata
- **Discovery run at**: 2026-06-17T00:00:00Z
- **Files scanned**: 83 (excluding bin/, obj/, nazgul/, .claude/, .git/)
- **Project**: CryptoExchanges.Net (`morpheums/CryptoExchanges.Net` on GitHub)

## Project Classification
- **Type**: BROWNFIELD (HIGH confidence)
- **Version**: `0.1.0-preview.1` (NuGet library, Apache 2.0)
- **Language/Runtime**: C# 13 / .NET 10 (`net10.0`)
- **Framework**: Multi-project .NET SDK class library (no web framework)
- **Test framework**: xunit.v3 3.2.2 + FluentAssertions + NSubstitute

## Key Findings
- **Architecture**: Strict four-layer dependency chain — Core (zero deps) → Http (resilience pipeline) → Binance (implementation) → DI (registration). Any PR must preserve this chain.
- **DeltaMapper mandate**: All DTO-to-model mappings use the maintainer's own DeltaMapper library (memory note: `use-deltamapper-for-object-mapping.md`). AutoMapper or manual mapping is not acceptable.
- **Signing pipeline**: HMAC-SHA256 signing is implemented as a `DelegatingHandler` with mark-and-strip to prevent duplicate signatures on retry. This pattern must be followed for all new exchanges.
- **TreatWarningsAsErrors + AnalysisLevel=latest-all**: Every PR must build cleanly. All public members need XML docs. This is a build-time check, not a style suggestion.
- **No CI/CD detected**: No GitHub Actions, no CI pipeline. Tests must be run locally before review.

## Existing Documentation
- **Count**: 2 docs found
- **Quality**: PARTIAL
- `README.md` — comprehensive consumer-facing guide (HIGH relevance)
- `LICENSE` — Apache 2.0 (LOW relevance)
- Gaps: no CHANGELOG, no architecture ADRs, no per-package docs, no API reference

## Agents Generated

### Reviewer Agents (4)
| Agent | Rationale |
|-------|-----------|
| `architect-reviewer` | Enforces strict layer boundaries, DeltaMapper mandate, composition root pattern |
| `code-reviewer` | Enforces C#/nullable/TreatWarningsAsErrors conventions, XML docs, guard pattern |
| `security-reviewer` | Reviews HMAC signing integrity, credential handling, rate limit coverage |
| `api-reviewer` | Reviews public NuGet API surface, breaking changes, NuGet package conventions |

### Specialist Agents (0)
No specialists generated — this is a pure .NET library with no frontend, database, mobile, or cloud infrastructure.

### Post-Loop Agents (2)
| Agent | Rationale |
|-------|-----------|
| `documentation` | Maintains README, creates CHANGELOG, ensures XML doc coverage after each task |
| `release-manager` | Manages version bumps in `Directory.Build.props`, NuGet pack validation, CHANGELOG, git tags |

**Total agents generated**: 6 (4 reviewers + 0 specialists + 2 post-loop)

## Warnings / Gaps
- No CI/CD pipeline detected — tests are not automatically run on PRs. Recommend adding GitHub Actions.
- No CHANGELOG exists — the release-manager agent will create one on first post-loop run.
- `Microsoft.Extensions.Logging.Abstractions` is declared as a dependency in Core but no `ILogger<T>` injection is used in services yet — logging is not implemented.
- Integration tests in `CryptoExchanges.Net.Binance.Tests.Integration` require live Binance API connectivity (or env vars). The `test_command` in config.json filters these out (`--filter 'Category!=Integration'`). Review if xunit categories are actually applied to these tests.
