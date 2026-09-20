using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using System.Net;

namespace DemoMAFHarness.Tools;

internal static class WebIqMcpToolProvider
{
    public static async Task<WebIqMcpToolScope> CreateToolScopeAsync(
        IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration[Settings.WebIqApiKeyKey]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Missing WebIQ credential. Set {Settings.WebIqApiKeyKey} in secrets.settings.json.");
        }

        var configuredEndpoint = configuration[Settings.WebIqMcpEndpointKey] ?? Settings.WebIqMcpEndpoint;
        if (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            throw new InvalidOperationException($"Invalid WebIQ endpoint. {Settings.WebIqMcpEndpointKey} must be an absolute HTTPS URL without embedded credentials.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Settings.WebIqTimeout);
        IAsyncDisposable? cleanupOnFailure = null;
        try
        {
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Name = Settings.WebIqServerName,
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    [Settings.WebIqApiKeyHeader] = apiKey,
                },
            });
            cleanupOnFailure = transport;

            var client = await McpClient.CreateAsync(transport,
                new McpClientOptions { InitializationTimeout = Settings.WebIqTimeout },
                cancellationToken: deadline.Token).ConfigureAwait(false);
            cleanupOnFailure = client;

            var tools = await client.ListToolsAsync(cancellationToken: deadline.Token).ConfigureAwait(false);
            var searchTool = tools.FirstOrDefault(tool => tool.Name == Settings.WebIqRemoteToolName);
            if (searchTool is null)
            {
                throw new MissingMemberException();
            }

            return new WebIqMcpToolScope(client, new WebIqGroundingTool(searchTool));
        }
        catch (Exception error)
        {
            if (cleanupOnFailure is not null)
            {
                // Preserve the setup failure without exposing transport exception details.
                try { await cleanupOnFailure.DisposeAsync().ConfigureAwait(false); }
                catch { }
            }

            cancellationToken.ThrowIfCancellationRequested();
            // Do not include remote exception messages or inner exceptions: they may contain request details.
            throw new InvalidOperationException(error switch
            {
                OperationCanceledException => "WebIQ connection or tool discovery timed out. Try again or check the configured endpoint.",
                HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
                    $"WebIQ rejected the credential. Check {Settings.WebIqApiKeyKey} and its access to the MCP endpoint.",
                HttpRequestException { StatusCode: HttpStatusCode.UnsupportedMediaType } =>
                    "WebIQ rejected the Streamable HTTP transport content type.",
                MissingMemberException => $"WebIQ did not expose the required '{Settings.WebIqRemoteToolName}' grounding tool.",
                _ => "Could not connect to WebIQ or discover its grounding tool. Check the endpoint, credential, and network connection.",
            });
        }
    }
}

internal sealed class WebIqMcpToolScope(IAsyncDisposable client, AIFunction groundingTool) : IAsyncDisposable
{
    public AIFunction GroundingTool { get; } = groundingTool;

    public async ValueTask DisposeAsync()
    {
        try { await client.DisposeAsync().ConfigureAwait(false); }
        catch { throw new InvalidOperationException("WebIQ connection cleanup failed."); }
    }
}
