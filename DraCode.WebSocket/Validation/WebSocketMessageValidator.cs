using Birko.Validation.Fluent;
using DraCode.WebSocket.Models;

namespace DraCode.WebSocket.Validation;

public class WebSocketMessageValidator : AbstractValidator<WebSocketMessage>
{
    private static readonly string[] ValidCommands =
        ["list", "connect", "disconnect", "reset", "send", "prompt_response"];

    public WebSocketMessageValidator()
    {
        RuleFor(x => x.Command)
            .Required("Command is required.")
            .Must(cmd => cmd != null && ValidCommands.Contains(cmd.ToLowerInvariant()),
                  $"Command must be one of: {string.Join(", ", ValidCommands)}.");

        RuleFor(x => x.AgentId)
            .Must(agentId => agentId is null || agentId.Length <= 100,
                  "AgentId must not exceed 100 characters.")
            .MustSatisfy(msg =>
                string.Equals(msg.Command, "list", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(msg.AgentId),
                "AgentId is required for non-list commands.");

        RuleFor(x => x.Data)
            .MaxLength(100_000, "Data must not exceed 100000 characters.");
    }
}
