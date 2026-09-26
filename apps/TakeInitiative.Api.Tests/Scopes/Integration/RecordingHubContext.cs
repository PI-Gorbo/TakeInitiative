using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>One message the API sent through the hub: the target groups, the name and the payload.</summary>
public record HubMessage(IReadOnlyList<string> Groups, string Method, object? Payload);

/// <summary>
/// An <see cref="IHubContext{CampaignHub}"/> that records every message instead of sending it.
/// Only group sends are expected; anything else is recorded with a marker group so a test sees it.
/// Group membership changes are ignored.
/// </summary>
public class RecordingHubContext : IHubContext<CampaignHub>, IHubClients, IGroupManager
{
    private readonly ConcurrentQueue<HubMessage> _messages = new();

    public IReadOnlyList<HubMessage> Messages => _messages.ToList();

    IHubClients IHubContext<CampaignHub>.Clients => this;
    IGroupManager IHubContext<CampaignHub>.Groups => this;

    private IClientProxy To(params string[] groups) => new Proxy(this, groups);

    public IClientProxy All => To("<all>");
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => To("<all-except>");
    public IClientProxy Client(string connectionId) => To($"<connection:{connectionId}>");
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => To("<connections>");
    public IClientProxy Group(string groupName) => To(groupName);
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => To($"<except>{groupName}");
    IClientProxy IHubClients<IClientProxy>.Groups(IReadOnlyList<string> groupNames) => To([.. groupNames]);
    public IClientProxy User(string userId) => To($"<user:{userId}>");
    public IClientProxy Users(IReadOnlyList<string> userIds) => To("<users>");

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private class Proxy(RecordingHubContext owner, string[] groups) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            owner._messages.Enqueue(new HubMessage(groups, method, args.Length > 0 ? args[0] : null));
            return Task.CompletedTask;
        }
    }
}

/// <summary>The authenticated fixture with <see cref="RecordingHubContext"/> in place of the real hub context.</summary>
public class RecordingHubFixture : AuthenticatedWebAppWithDatabaseFixture
{
    public RecordingHubContext Hub { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
        => services.AddSingleton<IHubContext<CampaignHub>>(Hub);
}
