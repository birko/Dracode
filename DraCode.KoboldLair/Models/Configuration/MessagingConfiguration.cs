namespace DraCode.KoboldLair.Models.Configuration;

/// <summary>
/// Configuration for message queue-based task dispatch.
/// When enabled, Drake publishes task assignments to a queue
/// and KoboldWorkerService consumes them.
/// </summary>
public class MessagingConfiguration
{
    /// <summary>
    /// Whether message queue dispatch is enabled (default: false = direct in-process).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Message queue backend: "InMemory" (dev/testing) or "MQTT" (production).
    /// </summary>
    public string Backend { get; set; } = "InMemory";

    /// <summary>
    /// Queue name configuration for task routing.
    /// </summary>
    public QueueNames Queues { get; set; } = new();

    /// <summary>
    /// MQTT settings (only used when Backend = "MQTT").
    /// </summary>
    public MqttConfig? Mqtt { get; set; }
}

public class QueueNames
{
    public string TaskAssignment { get; set; } = "kobold.tasks.assign";
    public string TaskCompletion { get; set; } = "kobold.tasks.complete";
    public string Heartbeat { get; set; } = "kobold.heartbeat";
}

public class MqttConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; }
    public string? ClientId { get; set; }
}
