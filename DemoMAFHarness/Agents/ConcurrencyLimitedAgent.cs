using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DemoMAFHarness.Agents;

/// <summary>Limits simultaneous worker runs while allowing any number of queued research assignments.</summary>
internal sealed class ConcurrencyLimitedAgent(AIAgent innerAgent, int maxConcurrentRuns)
    : DelegatingAIAgent(innerAgent), IDisposable
{
    private readonly SemaphoreSlim _slots = new(maxConcurrentRuns, maxConcurrentRuns);

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, CancellationToken cancellationToken)
    {
        await this._slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await base.RunCoreAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this._slots.Release();
        }
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await this._slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (var update in base.RunCoreStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            this._slots.Release();
        }
    }

    // The caller releases background sessions before disposing this gate and the separately owned chat client.
    public void Dispose() => this._slots.Dispose();
}
