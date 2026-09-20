using Harness.Shared.Console.ToolFormatters;
using Microsoft.Extensions.AI;

namespace DemoMAFHarness.Tools;

/// <summary>Displays a live notice before invoking a local tool.</summary>
internal sealed class ToolCallDisplayFunction(AIFunction innerFunction, Action<string> writeNotice)
    : DelegatingAIFunction(innerFunction)
{
    private readonly FallbackToolFormatter _formatter = new();

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var call = new FunctionCallContent(string.Empty, Name, arguments);
        var detail = _formatter.FormatDetail(call);
        var label = detail is null ? Name : $"{Name} {detail}";
        writeNotice($"🔧 Calling tool: {label}...");

        return base.InvokeCoreAsync(arguments, cancellationToken);
    }
}
