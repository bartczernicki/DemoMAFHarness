using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace DemoMAFHarness.Tools;

/// <summary>Locally invokes WebIQ's discovered MCP search function without exposing credentials to the model.</summary>
internal sealed class WebIqGroundingTool(McpClientTool remoteTool) : AIFunction
{
    public override string Name => Settings.WebIqToolName;

    public override string Description =>
        "Search the live web with WebIQ to ground research in source content and URLs. " + remoteTool.Description;

    public override JsonElement JsonSchema => remoteTool.JsonSchema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Settings.WebIqTimeout);
        try
        {
            // McpClientTool returns its complete CallToolResult as JSON, preserving structured content and citations.
            var result = await remoteTool.InvokeAsync(arguments, deadline.Token).ConfigureAwait(false);
            if (result is JsonElement json && json.TryGetProperty("isError", out var isError)
                && isError.ValueKind == JsonValueKind.True)
            {
                return "Error: WebIQ could not complete this grounding request. Try another query or disclose the verification gap.";
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Both caller cancellation and the per-call deadline stay cancellation, without remote exception details.
            throw new OperationCanceledException("WebIQ grounding was canceled or exceeded its time limit.",
                cancellationToken.IsCancellationRequested ? cancellationToken : deadline.Token);
        }
        catch
        {
            return "Error: WebIQ grounding is unavailable for this request. Try again or disclose that live verification failed.";
        }
    }
}
