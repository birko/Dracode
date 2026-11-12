using Birko.Communication.WebSocket.Messaging;

namespace DraCode.KoboldLair.Server.Services;

/// <summary>
/// <c>KoboldLair:WebSocket</c>: limits shared by the <c>/dragon</c>, <c>/wyvern</c> and <c>/kobold</c> endpoints.
/// </summary>
public sealed class WebSocketLimits
{
    public const string SectionName = "KoboldLair:WebSocket";

    /// <summary>Birko's default, 4 MiB: far above a chat turn, a command or a run request, even with a pasted specification.</summary>
    public const int DefaultMaxMessageBytes = WebSocketMessageExtensions.DefaultMaxMessageBytes;

    /// <summary>Below this an ordinary Dragon message with some pasted text would be refused.</summary>
    public const int MinMaxMessageBytes = 16 * 1024;

    /// <summary>
    /// The highest cap a configuration may set. Each connection may hold one message this large in memory while it
    /// arrives, so the cap bounds what a client can make the server allocate.
    /// </summary>
    public const int MaxMaxMessageBytes = 16 * 1024 * 1024;

    /// <summary>
    /// The largest message a client may send. A larger one closes the socket with MessageTooBig and is never parsed
    /// (TASK-135). Must lie between <see cref="MinMaxMessageBytes"/> and <see cref="MaxMaxMessageBytes"/>; the server
    /// refuses to start otherwise.
    /// </summary>
    public int MaxMessageBytes { get; set; } = DefaultMaxMessageBytes;

    public bool IsValid => MaxMessageBytes is >= MinMaxMessageBytes and <= MaxMaxMessageBytes;

    public static string InvalidMessage =>
        $"{SectionName}:MaxMessageBytes must be between {MinMaxMessageBytes} (16 KiB) and {MaxMaxMessageBytes} (16 MiB).";
}
