using System.Collections.Concurrent;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// A Marten listener that, when armed, runs a write of its own just before the next session
/// saves. The write commits first, so the session's append loses the race on the stream
/// version. That makes "another write landed between our read and our append" deterministic.
/// </summary>
public class InterferingSaveListener : DocumentSessionListenerBase
{
    private readonly ConcurrentQueue<Func<IDocumentStore, Task>> _pending = new();
    private readonly AsyncLocal<bool> _interfering = new();

    /// <summary>Runs <paramref name="write"/> before the next save. Arm it more than once to interfere with a retry too.</summary>
    public void BeforeNextSave(Func<IDocumentStore, Task> write) => _pending.Enqueue(write);

    public int Pending => _pending.Count;

    public override async Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken token)
    {
        // The interfering write's own save comes through here too: let it pass.
        if (_interfering.Value || !_pending.TryDequeue(out var write))
        {
            return;
        }
        _interfering.Value = true;
        try
        {
            await write(session.DocumentStore);
        }
        finally
        {
            _interfering.Value = false;
        }
    }
}

/// <summary>The recording-hub fixture with an <see cref="InterferingSaveListener"/> on the document store.</summary>
public class InterferingSaveFixture : RecordingHubFixture
{
    public InterferingSaveListener Interference { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.ConfigureMarten(opts => opts.Listeners.Add(Interference));
    }
}
