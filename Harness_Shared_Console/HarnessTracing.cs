// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable VSTHRD002 // Synchronous waits are required by OpenTelemetry enrichment callbacks.

using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Net.Http.Headers;

namespace Harness.Shared.Console;

/// <summary>
/// Provides factory methods for creating pre-configured OpenTelemetry tracing for harness samples.
/// </summary>
public static class HarnessTracing
{
    /// <summary>
    /// Creates a <see cref="TracerProvider"/> that captures spans from the specified source and HTTP client activity,
    /// enriching HTTP spans with request/response headers (with credentials redacted) and bodies,
    /// and exports all spans to a timestamped text file.
    /// </summary>
    /// <param name="sourceName">The activity source name to subscribe to (e.g., "Harness.Research").</param>
    /// <param name="outputDirectory">The log directory; defaults to the application base directory when omitted.</param>
    /// <returns>A configured <see cref="TracerProvider"/>, or <see langword="null"/> if the builder returns null.</returns>
    public static TracerProvider? CreateFileTracerProvider(string sourceName, string? outputDirectory = null)
    {
        var traceLogPath = Path.Combine(outputDirectory ?? AppContext.BaseDirectory, $"traces_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid()}.log");

        return Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .AddHttpClientInstrumentation((options) =>
            {
                options.EnrichWithHttpRequestMessage = (activity, request) =>
                {
                    activity.SetTag("http.request.headers", FormatHeaders(request.Headers));
                    if (request.Content != null)
                    {
                        activity.SetTag("http.request.content.headers", FormatHeaders(request.Content.Headers));
                        var content = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        activity.SetTag("http.request.content.body", content);
                    }
                };

                options.EnrichWithHttpResponseMessage = (activity, response) =>
                {
                    activity.SetTag("http.response.headers", FormatHeaders(response.Headers));
                    if (response.Content != null)
                    {
                        activity.SetTag("http.response.content.headers", FormatHeaders(response.Content.Headers));
                        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        activity.SetTag("http.response.content.body", content);
                    }
                };
            })
            .AddProcessor(new SimpleActivityExportProcessor(new FileSpanExporter(traceLogPath)))
            .Build();
    }

    private static string FormatHeaders(HttpHeaders headers) =>
        string.Join(Environment.NewLine, headers.Select(header =>
        {
            bool isCredential = header.Key.ToLowerInvariant() is
                "authorization" or "proxy-authorization" or "api-key" or "x-api-key" or
                "ocp-apim-subscription-key" or "cookie" or "set-cookie";
            return $"{header.Key}: {(isCredential ? "[REDACTED]" : string.Join(", ", header.Value))}";
        }));
}
