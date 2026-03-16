using Birko.EventBus;

namespace DraCode.WebSocket.Events;

public enum AgentConnectionAction
{
    Connected,
    Disconnected,
    Reset
}

public sealed record AgentConnectionEvent : EventBase
{
    public required string ConnectionId { get; init; }
    public required string AgentId { get; init; }
    public required string AgentType { get; init; }
    public required AgentConnectionAction Action { get; init; }

    public override string Source => "DraCode.WebSocket";
}
