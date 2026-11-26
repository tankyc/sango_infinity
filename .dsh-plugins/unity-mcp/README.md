# Unity MCP connection

Connects the [MCP for Unity](https://github.com/CoplayDev/unity-mcp) server running inside the Unity Editor to DeepSeek Harness as a streamable-HTTP MCP server.

- Endpoint: `http://127.0.0.1:8080/mcp`
- Server name: `unityMCP`, so tools appear as `mcp__unityMCP__<tool>`
- Transport: `streamable-http`

## Requirements

1. The Unity Editor is open on the target project.
2. The MCP for Unity bridge is running in that editor, listening on `127.0.0.1:8080`
   (`GET http://127.0.0.1:8080/health` answers `{"status":"healthy",...}`).

## Behavior

The plugin activates the `@deepseek-ai/dsh-mcp-client` row from `cordis.patch.yml` and
waits for the initial tool discovery. With `failOnStartupError: true`, a Host start while
the editor is closed leaves this row inactive and logs the failure; start the editor and
turn the plugin off/on in Plugins, or restart DeepSeek Harness.

Turning the plugin off unmounts the client and removes every `mcp__unityMCP__` tool.

## Change the endpoint

Edit `cordis.patch.yml` (`url`), then disable and re-enable the plugin in Plugins.
