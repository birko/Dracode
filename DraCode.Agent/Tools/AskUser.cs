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

        public Func<string, string, Task<string>>? PromptCallback { get; set; }

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            try
            {
                var question = input["question"].ToString();
                var context = input.TryGetValue("context", out var contextVal) ? contextVal?.ToString() : null;

                if (string.IsNullOrWhiteSpace(question))
                    return "Error: question parameter is required";

                // Check if we're in non-interactive mode
                if (Options != null && !Options.Interactive)
                {
                    // Non-interactive mode - return default response or error
                    if (!string.IsNullOrEmpty(Options.DefaultPromptResponse))
                    {
                        SendMessage("info", $"[Non-Interactive Mode] Auto-responding to prompt: {question}");
                        SendMessage("info", $"[Non-Interactive Mode] Response: {Options.DefaultPromptResponse}");
                        return Options.DefaultPromptResponse;
                    }
                    else
                    {
                        SendMessage("warning", $"[Non-Interactive Mode] Prompt requested but no default response configured: {question}");
                        return "Error: Cannot prompt for user input in non-interactive mode. Configure DefaultPromptResponse or enable interactive mode.";
                    }
                }

                // Interactive mode
                // If we have a prompt callback (WebSocket mode), use it
                if (PromptCallback != null)
                {
                    var fullPrompt = string.IsNullOrWhiteSpace(context) 
                        ? question 
                        : $"Context: {context}\n\nQuestion: {question}";
                    
                    SendMessage("prompt", fullPrompt ?? "");
                    
                    // Use timeout if specified
                    var timeout = Options?.PromptTimeout ?? 300;
                    var promptTask = PromptCallback(question ?? "", context ?? "");
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeout));
                    
                    var completedTask = Task.WhenAny(promptTask, timeoutTask).ConfigureAwait(false).GetAwaiter().GetResult();
                    
                    if (completedTask == timeoutTask)
                    {
                        SendMessage("warning", $"Prompt timed out after {timeout} seconds");
                        return $"Error: Prompt timed out after {timeout} seconds. No response received from user.";
                    }
                    
                    var response = promptTask.ConfigureAwait(false).GetAwaiter().GetResult();
                    return response;
                }

                // Console/callback mode - just send the prompt message
                var promptMsg = string.IsNullOrWhiteSpace(context)
                    ? $"Question: {question}"
                    : $"Context: {context}\n\nQuestion: {question}";
                
                SendMessage("prompt_console", promptMsg);
                
                // In console mode without AnsiConsole, we can't actually get input
                // The host application (Program.cs) should handle this
                return "Error: Console input not available in library mode. Use PromptCallback for interactive prompts.";
            }
            catch (Exception ex)
            {
                return $"Error getting user input: {ex.Message}";
            }
        }
    }
}
