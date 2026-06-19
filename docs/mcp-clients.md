# MCP Client Setup

> **Note:** Config formats evolve — check your client's current MCP docs if a key differs.

This page covers per-client configuration for `crypto-mcp`, the read-only
[CryptoExchanges.Net MCP server](mcp-server.md). Every client launches the same local stdio
command (`crypto-mcp`); only the config file location and key shape vary.

Requires .NET 10 SDK and `crypto-mcp` installed globally — see
[mcp-server.md — Install](mcp-server.md#install).

Market-data tools require **no API credentials**. Account tools require read-permission API keys
set via environment variables — see [mcp-server.md — Credentials](mcp-server.md#credentials)
for the full env-var reference.

---

## Claude Code (CLI)

Add the server for your current project or globally:

```bash
# Project scope (stored in .mcp.json in the repo root)
claude mcp add crypto -- crypto-mcp

# User scope (available in all projects)
claude mcp add --scope user crypto -- crypto-mcp
```

To pass exchange credentials, add `--env` flags **before** the `--` separator (everything after
`--` is passed to the `crypto-mcp` subprocess, not to `claude mcp add`):

```bash
claude mcp add crypto \
  --env BINANCE_API_KEY=your_key \
  --env BINANCE_SECRET_KEY=your_secret \
  -- crypto-mcp
```

Or set them in your shell environment before running Claude Code — they are inherited by the
subprocess.

---

## Claude Desktop

Config file location:

- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

Add the `crypto` server under `mcpServers`:

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp"
    }
  }
}
```

To use account tools, add an `env` block with your exchange credentials:

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp",
      "env": {
        "BINANCE_API_KEY": "your_binance_api_key",
        "BINANCE_SECRET_KEY": "your_binance_secret_key",
        "BYBIT_API_KEY": "your_bybit_api_key",
        "BYBIT_SECRET_KEY": "your_bybit_secret_key",
        "OKX_API_KEY": "your_okx_api_key",
        "OKX_SECRET_KEY": "your_okx_secret_key",
        "OKX_PASSPHRASE": "your_okx_passphrase",
        "BITGET_API_KEY": "your_bitget_api_key",
        "BITGET_SECRET_KEY": "your_bitget_secret_key",
        "BITGET_PASSPHRASE": "your_bitget_passphrase"
      }
    }
  }
}
```

You only need to include credentials for exchanges you intend to use. Restart Claude Desktop
after editing the config.

---

## Cursor

Config file location (pick one):

- **Project**: `.cursor/mcp.json` (repo root)
- **Global**: `~/.cursor/mcp.json`

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp"
    }
  }
}
```

With account credentials:

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp",
      "env": {
        "BINANCE_API_KEY": "your_binance_api_key",
        "BINANCE_SECRET_KEY": "your_binance_secret_key"
      }
    }
  }
}
```

---

## VS Code (GitHub Copilot, agent mode)

Config file: `.vscode/mcp.json` in the workspace root.

VS Code uses a `servers` key (not `mcpServers`) and requires an explicit `"type": "stdio"`:

```json
{
  "servers": {
    "crypto": {
      "type": "stdio",
      "command": "crypto-mcp"
    }
  }
}
```

With account credentials:

```json
{
  "servers": {
    "crypto": {
      "type": "stdio",
      "command": "crypto-mcp",
      "env": {
        "BINANCE_API_KEY": "your_binance_api_key",
        "BINANCE_SECRET_KEY": "your_binance_secret_key"
      }
    }
  }
}
```

MCP tool access requires VS Code 1.99+ with GitHub Copilot in agent mode enabled.

---

## Windsurf

Config file: `~/.codeium/windsurf/mcp_config.json`

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp"
    }
  }
}
```

With account credentials:

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp",
      "env": {
        "BINANCE_API_KEY": "your_binance_api_key",
        "BINANCE_SECRET_KEY": "your_binance_secret_key"
      }
    }
  }
}
```

---

## Cline (VS Code extension)

Config file (managed via Cline's UI or directly):
`~/Library/Application Support/Code/User/globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json`

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp",
      "disabled": false,
      "autoApprove": []
    }
  }
}
```

With account credentials:

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp",
      "disabled": false,
      "autoApprove": [],
      "env": {
        "BINANCE_API_KEY": "your_binance_api_key",
        "BINANCE_SECRET_KEY": "your_binance_secret_key"
      }
    }
  }
}
```

You can also add servers through Cline's MCP settings panel in VS Code.

---

## OpenAI Codex CLI

Config file: `~/.codex/config.toml` (TOML format)

```toml
[mcp_servers.crypto]
command = "crypto-mcp"
```

With account credentials:

```toml
[mcp_servers.crypto]
command = "crypto-mcp"

[mcp_servers.crypto.env]
BINANCE_API_KEY = "your_binance_api_key"
BINANCE_SECRET_KEY = "your_binance_secret_key"
```

---

## Gemini CLI

Config file: `~/.gemini/settings.json`

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp"
    }
  }
}
```

With account credentials:

```json
{
  "mcpServers": {
    "crypto": {
      "command": "crypto-mcp",
      "env": {
        "BINANCE_API_KEY": "your_binance_api_key",
        "BINANCE_SECRET_KEY": "your_binance_secret_key"
      }
    }
  }
}
```

---

## Testing with MCP Inspector

Before connecting a full client you can verify the server works using the official
[MCP Inspector](https://github.com/modelcontextprotocol/inspector):

```bash
npx @modelcontextprotocol/inspector dotnet run --project src/CryptoExchanges.Net.Mcp -c Release
```

This opens a browser UI where you can call tools interactively and inspect request/response
envelopes before connecting a full client.

---

## Next steps

- [mcp-server.md](mcp-server.md) — full tool reference, env-var table, error categories
- [getting-started.md](getting-started.md) — install and use the .NET library directly
