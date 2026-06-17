# Project Profile

## Language(s)
- C# 13 / .NET 10: `Directory.Build.props:3` (`<TargetFramework>net10.0</TargetFramework>`)
  - Modern C# features confirmed: primary constructors (`BinanceHttpClient.cs:16`), records (`Models.cs:9`), required properties (`IExchangeClient.cs:181`), collection expressions (`SymbolMapper.cs:39`)

## Framework(s)
- .NET class library (no web framework): `CryptoExchanges.Net.Core.csproj:1` (`Sdk="Microsoft.NET.Sdk"`)
- Microsoft.Extensions.Http.Resilience (Polly v8 pipeline): `CryptoExchanges.Net.Http.csproj:5`
- Microsoft.Extensions.DependencyInjection (DI integration): `CryptoExchanges.Net.Core.csproj:13`

## Package Manager
- dotnet CLI / NuGet: `CryptoExchanges.Net.sln:1` (Visual Studio 17 solution format)

## Build System
- MSBuild / dotnet CLI: `Directory.Build.props` (shared properties inherited by all projects)
- No Makefile, Dockerfile, or docker-compose detected

## Monorepo Structure
- Multi-project solution with `src/`, `tests/`, `samples/` layout: `CryptoExchanges.Net.sln:6-26`
- Projects:
  - `src/CryptoExchanges.Net.Core` — abstractions, models, interfaces
  - `src/CryptoExchanges.Net.Binance` — Binance exchange implementation
  - `src/CryptoExchanges.Net.Http` — shared resilience pipeline
  - `src/CryptoExchanges.Net.DependencyInjection` — DI extensions
  - `tests/CryptoExchanges.Net.Core.Tests.Unit`
  - `tests/CryptoExchanges.Net.Http.Tests.Unit`
  - `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit`
  - `tests/CryptoExchanges.Net.Binance.Tests.Integration`
  - `samples/BasicUsage`

## Database
- None detected — no ORM, no database connection strings, no migration folders

## API Style
- REST (HTTP client consuming external exchange REST APIs): `BinanceHttpClient.cs:26-68`
- No server-side API surface (library, not a server)

## State Management
- None detected (not a frontend project)

## Deployment Target
- NuGet package: `Directory.Build.props:10-18` (Authors, PackageLicenseExpression, PackageProjectUrl, Version)
- No Dockerfile, Vercel, or cloud deployment configs

## Cloud Provider
- None detected (library; consumes Binance REST at `https://api.binance.com` — not hosted)

## IaC Tool
- None detected
- NOTE: No cloud provider hosting detected — IaC not applicable for a NuGet library

## Container Orchestration
- None detected

## CI/CD Platform
- None detected (no `.github/workflows/`, no `.gitlab-ci.yml`, no `.circleci/`)

## Observability
- Logging: `Microsoft.Extensions.Logging.Abstractions` declared — `CryptoExchanges.Net.Core.csproj:13`
- Monitoring: None detected
- Error tracking: None detected (exceptions are typed and surfaced to callers)

## Secret Management
- Environment variables: `BinanceExchangeClient.cs:93-94` (`BINANCE_API_KEY`, `BINANCE_SECRET_KEY`)
  and `ServiceCollectionExtensions.cs:116-120` (`ApplyEnvDefaults`)
- No `.env` files, vault, or secrets manager

## Key Dependencies
1. **DeltaMapper 1.2.0** — maintainer's own DTO→model object mapper (project mandate); `CryptoExchanges.Net.Binance.csproj:26`
2. **Microsoft.Extensions.Http.Resilience 10.7.0** — Polly v8 HTTP resilience pipeline (retry, timeout, rate limiting); `CryptoExchanges.Net.Http.csproj:5`
3. **Microsoft.Extensions.DependencyInjection.Abstractions 10.0*** — DI container integration; `CryptoExchanges.Net.Core.csproj:13`
4. **Microsoft.Extensions.Http 10.0*** — `IHttpClientFactory`, typed client registration; `CryptoExchanges.Net.Binance.csproj:28`
5. **Microsoft.Extensions.Options 10.0*** — strongly-typed options, `ValidateOnStart`; `CryptoExchanges.Net.Binance.csproj:29`
6. **Microsoft.Extensions.Logging.Abstractions 10.0*** — logging abstraction; `CryptoExchanges.Net.Core.csproj:12`
7. **xunit.v3 3.2.2** — test framework; `CryptoExchanges.Net.Core.Tests.csproj:17`
8. **FluentAssertions 6.12.2** — test assertion library; `CryptoExchanges.Net.Core.Tests.csproj:20`
9. **NSubstitute 5.3.0** — mocking framework; `CryptoExchanges.Net.Core.Tests.csproj:18`
10. **Microsoft.NET.Test.Sdk 18.6.0** — test runner; `CryptoExchanges.Net.Core.Tests.csproj:16`

## GitHub Integration
- **GitHub repo**: morpheums/CryptoExchanges.Net
- **gh CLI**: installed (`/opt/homebrew/bin/gh`)
- **project scope**: present (token scopes include `project`)
- **Existing projects**: 1 project found ("Strumtry Development Roadmap")
