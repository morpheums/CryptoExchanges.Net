# Test Strategy

## Test Framework
- xunit.v3 3.2.2: `CryptoExchanges.Net.Core.Tests.csproj:17` (`<PackageReference Include="xunit.v3" Version="3.2.2" />`)
- xunit.runner.visualstudio 3.1.5: `CryptoExchanges.Net.Core.Tests.csproj:18` (VS/IDE runner integration)
- Microsoft.NET.Test.Sdk 18.6.0: `CryptoExchanges.Net.Core.Tests.csproj:16` (dotnet test CLI runner)

## Test Location
- Separate `tests/` directory at solution root — `CryptoExchanges.Net.sln:12-17`
- Test projects are peer projects, NOT co-located with source files
- Naming convention: `[source-project-name].Tests.[Unit|Integration]`

## Test Types Present
- [x] Unit — `tests/CryptoExchanges.Net.Core.Tests.Unit/`, `tests/CryptoExchanges.Net.Http.Tests.Unit/`, `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/`
- [x] Integration — `tests/CryptoExchanges.Net.Binance.Tests.Integration/` (tests against real Binance API and live resilience pipeline with stub HTTP handlers; uses `InternalsVisibleTo` to access internal DTOs)
- [ ] E2E
- [ ] Snapshot
- [ ] Property-based

## Coverage Tool
- None detected — no `coverlet`, `reportgenerator`, or coverage runsettings in any csproj

## Test Commands
- `dotnet test` — runs all test projects in the solution
- `dotnet test tests/CryptoExchanges.Net.Core.Tests.Unit/` — unit tests only
- `dotnet test tests/CryptoExchanges.Net.Binance.Tests.Integration/` — integration tests only (requires live Binance connectivity for some tests; others use stub handlers)
- Tests have `<OutputType>Exe</OutputType>` — compiled as executables for xunit.v3 runner model

## Fixtures & Mocks
- **NSubstitute 5.3.0**: `CryptoExchanges.Net.Core.Tests.csproj:18` — interface mocking (e.g. `IBinanceHttpClient`, `IExchangeErrorTranslator`)
- **FluentAssertions 6.12.2**: `CryptoExchanges.Net.Core.Tests.csproj:20` — assertion DSL (`.Should().Be()`, `.Should().Throw<>()`, etc.)
- **Stub HTTP handlers**: hand-written `StubHandler.cs` in `CryptoExchanges.Net.Http.Tests.Unit/` and inline `SeqStub` in `BinancePipelineEndToEndTests.cs` — simulate specific HTTP response sequences for resilience pipeline testing
- **TestContext.Current.CancellationToken**: xunit.v3 test context used in integration tests — `BinancePipelineEndToEndTests.cs:40`
- Integration tests with live Binance connectivity use `BinanceExchangeClient.CreateFromEnvironment()` pattern (env vars `BINANCE_API_KEY`/`BINANCE_SECRET_KEY`)

## CI Integration
- None detected — no `.github/workflows/`, `.circleci/`, `.gitlab-ci.yml`, or `Jenkinsfile`
