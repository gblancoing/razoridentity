using ComunaClick.Api.Modules.Delivery;
using Microsoft.AspNetCore.SignalR;

namespace ComunaClick.Tests.Unit.TestSupport;

/// <summary>IHubContext de prueba que registra los broadcasts (grupo + método + payload).</summary>
internal sealed class RecordingDeliveryHubContext : IHubContext<DeliveryHub>
{
    public List<(string Group, string Method, object?[] Args)> Sent { get; } = new();

    public IHubClients Clients => new RecordingHubClients(this);
    public IGroupManager Groups { get; } = new NoopGroupManager();

    private sealed class RecordingHubClients : IHubClients
    {
        private readonly RecordingDeliveryHubContext _owner;

        public RecordingHubClients(RecordingDeliveryHubContext owner) => _owner = owner;

        public IClientProxy All => new RecordingClientProxy(_owner, "*");
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => All;
        public IClientProxy Client(string connectionId) => new RecordingClientProxy(_owner, connectionId);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => All;
        public IClientProxy Group(string groupName) => new RecordingClientProxy(_owner, groupName);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => All;
        public IClientProxy User(string userId) => new RecordingClientProxy(_owner, userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => All;
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        private readonly RecordingDeliveryHubContext _owner;
        private readonly string _target;

        public RecordingClientProxy(RecordingDeliveryHubContext owner, string target)
        {
            _owner = owner;
            _target = target;
        }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            _owner.Sent.Add((_target, method, args));
            return Task.CompletedTask;
        }
    }

    internal sealed class NoopGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
