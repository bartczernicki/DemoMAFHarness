# DemoMAFHarness

Run the .NET 10 console app from its project directory:

```powershell
cd DemoMAFHarness
dotnet run
```

Choose one demo per launch: a direct model call, a research analyst agent, or a research
harness starting in execute or plan mode. Use `/exit` to close a harness session.

## Configuration

The app reads `local.settings.json` and then `secrets.settings.json` from the working
directory. Store credentials in the latter; do not commit it. The required keys are:

| Key | Purpose |
| --- | --- |
| `AzureOpenAI:Endpoint` | Azure OpenAI resource endpoint |
| `AzureOpenAI:APIKey` | Model API credential |
| `AzureOpenAI:ModelDeploymentName` | Model deployment name |
| `AzureOpenAI:WebIQGroundingAPIKey` | WebIQ MCP credential, sent only in the `x-apikey` header |

`WebIQ:McpEndpoint` optionally overrides the default `https://api.microsoft.ai/v3/mcp/`.
It must be an absolute HTTPS URL without embedded credentials. For example, the default
can be specified in `local.settings.json` as:

```json
{
  "WebIQ": {
    "McpEndpoint": "https://api.microsoft.ai/v3/mcp/"
  }
}
```

Other app defaults live in `Settings.cs`; research instructions and the sample request
live in `Prompts/ResearchPrompts.cs`. All four demos use medium reasoning and a 200-second
AI network-request timeout; retries and multi-call research runs can take longer overall.
WebIQ setup and each grounding call have a 60-second
timeout. Selecting Exit does not connect to WebIQ or initialize telemetry.

## Research Tools

All four options expose the local `WebIQGrounding` function. It invokes the MCP server's
discovered `web` tool over Streamable HTTP, preserving its argument schema, source
content, and URLs. Options 1 and 2 automatically execute tool calls before completing
their responses.

Options 3 and 4 also retain `DownloadUri` for inspecting source pages as Markdown.
The harness considers WebIQ for source discovery and Download URI for deeper verification;
its built-in web search is disabled. Other remote MCP tools are not exposed. Reports use
the investment analyst format and Markdown source links; harness reports are also saved
to file memory.

Missing credentials, connection/authentication failures, or a missing `web` tool stop
startup with a concise error. Grounding failures are reported without remote exception
details, and cancellation is preserved. Research output must disclose any verification gaps.

## Telemetry

Each selected demo writes OpenTelemetry logs to `Telemetry` beside the executable.
Credential headers, including WebIQ's `x-apikey`, are redacted. SSE response bodies are
not buffered or logged; HTTP spans, headers, and nonstreaming bodies remain available.
Logs can contain research prompts and results. Existing log files are left untouched.

The MCP integration follows the [reference WebIQ provider](https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/src/BaseballAIWorkbench/BaseballAIWorkbench.ApiService/WebIqMcpToolProvider.cs)
and uses `ModelContextProtocol` 2.2.0.
