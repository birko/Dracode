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

                // Display the question to the user
                Console.WriteLine();
                Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
                Console.WriteLine("│ 🤔 Agent needs your input                               │");
                Console.WriteLine("└─────────────────────────────────────────────────────────┘");
                
                if (!string.IsNullOrWhiteSpace(context))
                {
                    Console.WriteLine();
                    Console.WriteLine($"Context: {context}");
                }
                
                Console.WriteLine();
                Console.WriteLine($"Question: {question}");
                Console.WriteLine();
                Console.Write("Your answer: ");

                // Read user input
                var userResponse = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userResponse))
                {
                    return "User provided no response (empty input)";
                }

                Console.WriteLine();
                return userResponse;
            }
            catch (Exception ex)
            {
                return $"Error getting user input: {ex.Message}";
            }
        }
    }
}
