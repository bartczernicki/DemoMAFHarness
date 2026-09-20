# DemoMAFHarness

Run the .NET 10 console app from its project directory:

```powershell
cd DemoMAFHarness
dotnet run
```

Choose one demo per launch: a direct model call, a research analyst agent, or a research
harness starting in execute or plan mode. Option **5) Research Harness — Background Agents**
runs parallel research before synthesizing a report. Use `/exit` to close a harness session.

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
live in `Prompts/ResearchPrompts.cs`. All five demos use medium reasoning and a 200-second
AI network-request timeout; retries and multi-call research runs can take longer overall.
All demos use the Responses API so reasoning and function tools work together on the configured model.
WebIQ setup and each grounding call have a 60-second
timeout. Selecting Exit does not connect to WebIQ or initialize telemetry.

## Research Tools

Options 1–4 and option 5's research workers expose the local `WebIQGrounding` function. It invokes the MCP server's
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

## Background Research

Option 5 starts in execute mode. Enter any investment topic, including the sample AI
infrastructure question. The `ResearchCoordinator` interprets natural-language requests,
lists, company/ticker sets, comparisons, and multiple questions, then chooses the number
of assignments needed. It merges duplicate topics, separates research subjects from
formatting instructions, and briefly explains the topic breakdown before delegating.
A narrow question can use one worker; broader requests can use as many complementary
assignments as needed, with further tasks added when evidence reveals a gap.

Each assignment runs on `ResearchWorker` in its own session using the framework's
`BackgroundAgentsProvider`. `Settings.MaxConcurrentResearchWorkers` limits simultaneous
worker runs to four by default; additional assignments queue and start as slots free up.
This limits concurrency, not the number of topics or total assignments. Queued work also
honors cancellation during session cleanup. Existing iteration and request limits still apply.

Workers use WebIQ for discovery and Download URI for original-page inspection. They
have no planning, todos, file memory, or further delegation. The coordinator uses the
background-task tools to start work, wait, retrieve results, and request focused
follow-ups. It reconciles evidence and saves and returns one report with the six
investment research sections and Markdown source links, disclosing unresolved gaps.
The final synthesis covers every requested topic and comparison, including their
relationships and tradeoffs where relevant.
Completed task records are cleared after their results have been incorporated.

All research instructions include the application's current UTC date. The coordinator
passes a consistent research as-of date to workers; an explicit historical cutoff in
the user's request takes precedence. Different fiscal periods or stale sources must be
explained rather than silently moving current research to an older baseline.

All demos use client-managed Responses history (`store: false`) with encrypted reasoning
included for subsequent turns. Worker continuations and coordinator tool loops therefore
do not depend on Azure retaining a `previous_response_id`. Start a new session after
upgrading; old session exports may still reference server-stored conversations.
API quotas still apply: repeated WebIQ or model throttling can leave verification gaps
or stop a run after the configured retries. Details are recorded in the Telemetry log.

The console shows background-task activity; worker model and tool details are recorded
in telemetry. The coordinator retains `/mode`, planning approval, todos, and session
commands. `/exit` also works while background research is running. Closing or replacing
a session cancels and releases its background work before clients are disposed. Fatal
coordinator failures close the session and cancel outstanding work.

This follows Microsoft's [background-agent research sample](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/02-agents/Harness/Harness_Step02_Research_WithBackgroundAgents/Program.cs),
adapted to the investment analyst prompts and WebIQ grounding. Agent names, concurrency,
and worker provider settings live in `Settings.cs`.

## Telemetry

Each selected demo writes OpenTelemetry logs to `Telemetry` beside the executable.
Credential headers, including WebIQ's `x-apikey`, are redacted. SSE response bodies are
not buffered or logged; HTTP spans, headers, and nonstreaming bodies remain available.
Logs can contain research prompts and results. Existing log files are left untouched.

The MCP integration follows the [reference WebIQ provider](https://github.com/bartczernicki/MachineLearning-BaseballPrediction-BlazorApp/blob/master/src/BaseballAIWorkbench/BaseballAIWorkbench.ApiService/WebIqMcpToolProvider.cs)
and uses `ModelContextProtocol` 2.2.0.
