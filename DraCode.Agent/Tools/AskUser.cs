using Spectre.Console;

namespace DraCode.Agent.Tools
{
    public class AskUser : Tool
    {
        public override string Name => "ask_user";
        
        public override string Description => "Ask the user for additional input or clarification when you need information that you cannot obtain through other tools. Use this when you need user decisions, preferences, missing details, or confirmations. The user will see your question and can provide a response.";
        
        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                question = new
                {
                    type = "string",
                    description = "The question or prompt to show the user. Be clear and specific about what information you need."
                },
                context = new
                {
                    type = "string",
                    description = "Optional context or explanation to help the user understand why you're asking"
                }
            },
            required = new[] { "question" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            try
            {
                var question = input["question"].ToString();
                var context = input.TryGetValue("context", out var contextVal) ? contextVal?.ToString() : null;

                if (string.IsNullOrWhiteSpace(question))
                    return "Error: question parameter is required";

                // Create a styled panel for the question
                var panelContent = new Markup($"[bold cyan]❓ Question:[/] [white]{Markup.Escape(question ?? "")}[/]");
                
                if (!string.IsNullOrWhiteSpace(context))
                {
                    panelContent = new Markup(
                        $"[dim]💡 Context:[/] [grey]{Markup.Escape(context)}[/]\n\n" +
                        $"[bold cyan]❓ Question:[/] [white]{Markup.Escape(question ?? "")}[/]");
                }

                var panel = new Panel(panelContent)
                {
                    Border = BoxBorder.Double,
                    BorderStyle = new Style(Color.Cyan1),
                    Header = new PanelHeader("[bold yellow]🤔 Agent Needs Your Input[/]", Justify.Center),
                    Padding = new Padding(2, 1)
                };

                AnsiConsole.Write(panel);
                AnsiConsole.WriteLine();

                // Read user input with a nice prompt
                var userResponse = AnsiConsole.Ask<string>("[bold green]Your answer:[/]");

                if (string.IsNullOrWhiteSpace(userResponse))
                {
                    return "User provided no response (empty input)";
                }

                // Show confirmation
                AnsiConsole.MarkupLine($"[dim]✓ Received:[/] [white]{Markup.Escape(userResponse)}[/]");
                AnsiConsole.WriteLine();

                return userResponse;
            }
            catch (Exception ex)
            {
                return $"Error getting user input: {ex.Message}";
            }
        }
    }
}
