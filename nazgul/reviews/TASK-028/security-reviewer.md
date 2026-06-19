# Security Review — TASK-028

## Verdict: APPROVED

## Score: 96/100

## Findings

### NOTE Stderr routing uses LogToStandardErrorThreshold — correct but worth documenting (confidence: 72%)
`Program.cs:10` sets `LogToStandardErrorThreshold = LogLevel.Trace`, which routes ALL log entries at or above Trace to stderr. This is the correct approach for MCP stdio transport where stdout is the protocol channel. The Microsoft.Extensions.Logging console provider writes to stderr when the severity meets or exceeds the threshold, so every log message (Trace and above) goes to stderr. No log traffic touches stdout. No follow-up action required; confidence is below 80 so this is non-blocking.

### NOTE CryptoExchangesOptions has no ToString() override and no serialization attributes (confidence: 95%)
`CryptoExchangesOptions.cs` (pre-existing, unchanged by this diff) holds all secret key properties as plain auto-properties with no `[JsonInclude]`, no `[JsonPropertyName]`, no custom `ToString()`. The class is sealed with no serialization-friendly constructor. Secret fields will appear in any accidental JSON serialization via default reflection. This is a pre-existing condition not introduced by TASK-028 and the MCP scaffold does not add any serialization path for the options object. Flagged as NOTE for awareness. No action required from this task.

### PASS Credentials sourced exclusively from environment variables
`EnvCredentialBinder.Apply` accepts a `Func<string, string?>` delegate. In `Program.cs` it is wired directly to `Environment.GetEnvironmentVariable`. No hardcoded keys, no config files, no command-line args, no stdin reads are present in the diff.

### PASS No credential values appear in any log message
Neither `Program.cs` nor `EnvCredentialBinder.cs` passes any option property value to a logger. The only logger interaction in `Program.cs` is the threshold configuration call. `EnvCredentialBinder.Apply` writes env var values directly to properties on the options object with no intermediate string capture or log call.

### PASS EnvCredentialBinder does not store or cache secrets
`Apply` is a static void method. It reads env vars and assigns them immediately to the options properties. No fields, no closures, no static caches. The options object lifetime is controlled by the DI container configured in `Program.cs`, consistent with the existing pattern in `BinanceExchangeClient.cs:93-94`.

### PASS stdout is the MCP transport channel and no log provider writes to it
`WithStdioServerTransport()` owns stdout. The console logger is configured with `LogToStandardErrorThreshold = LogLevel.Trace`, which means the console provider routes all levels to stderr. `Host.CreateApplicationBuilder` wires the default console provider; the explicit `AddConsole` call with the threshold override replaces the default configuration, ensuring no log output reaches stdout.

### PASS Test credentials are clearly dummy values
`EnvCredentialBinderTests.cs` uses two-character placeholder values ("bk", "bs", "yk", "ys", "ok", "os", "op", "gk", "gs", "gp"). These are unambiguously synthetic; no real-looking API key formats (32+ hex chars, base64 strings) appear. No real environment variables are set in the test process; the tests inject a mock dictionary via the `getEnv` delegate, so there is no env-var contamination and no cleanup is required.

### PASS No serialization path for secrets in the new MCP code
The diff introduces no `JsonSerializer.Serialize`, no `ToString()` on options, no HTTP-layer serialization of credentials. `EnvCredentialBinder.Apply` only writes to options properties; it never reads them back or emits them.

### PASS No signing pipeline code in this diff
TASK-028 is a scaffold task only. No `DelegatingHandler`, no signing handler, no `MarkSigned()` call, no query string construction is introduced. Signing pipeline integrity is not at risk from this change.

## Summary

TASK-028 delivers a clean MCP host scaffold with a narrow, well-isolated credential surface. Credentials are read exclusively from environment variables through an injectable `Func<string, string?>` delegate (testable without touching real env vars), assigned once to the options object, and never logged, serialized, or cached. The stdout/stderr separation is correctly handled: `LogToStandardErrorThreshold = LogLevel.Trace` routes all console log output to stderr, leaving stdout exclusively for the MCP stdio transport. Test values are obviously synthetic, and the delegate-injection design means tests require no real env vars and no cleanup. The only pre-existing gap — `CryptoExchangesOptions` lacking a secret-redacting `ToString()` — is not introduced or worsened by this task. No blocking findings were identified.
