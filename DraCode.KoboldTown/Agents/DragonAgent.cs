using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldTown.Agents
{
    /// <summary>
    /// DragonAgent is a specialized agent for gathering project/task requirements from users.
    /// It conducts interactive discussions to understand what needs to be built and produces
    /// detailed specifications that can be used by the wyvern and other KoboldTown agents.
    /// </summary>
    public class DragonAgent : AgentBase
    {
        private readonly string _specificationsPath;

        protected override string SystemPrompt => GetDragonSystemPrompt();

        /// <summary>
        /// Creates a new Dragon agent for requirements gathering
        /// </summary>
        /// <param name="provider">LLM provider to use</param>
        /// <param name="options">Agent options</param>
        /// <param name="specificationsPath">Path where specifications should be stored (default: ./specifications)</param>
        public DragonAgent(
            ILlmProvider provider,
            AgentOptions? options = null,
            string specificationsPath = "./specifications")
            : base(provider, options)
        {
            _specificationsPath = specificationsPath;
            
            // Ensure specifications directory exists
            if (!Directory.Exists(_specificationsPath))
            {
                Directory.CreateDirectory(_specificationsPath);
            }
        }

        /// <summary>
        /// Creates the tools available to Dragon, including the specification writer
        /// </summary>
        protected override List<Tool> CreateTools()
        {
            var tools = base.CreateTools();
            tools.Add(new SpecificationWriterTool(_specificationsPath));
            return tools;
        }

        /// <summary>
        /// Gets the specialized system prompt for the Dragon agent
        /// </summary>
        private string GetDragonSystemPrompt()
        {
            return @"You are Dragon 🐉, a senior requirements analyst and project architect for KoboldTown.

Your role is to have an interactive conversation with the user to deeply understand their project or task requirements, then create a comprehensive specification document.

## Your Process:

1. **Greet and Understand Context**
   - Introduce yourself warmly as Dragon, their requirements guide
   - Ask what project or task they want to work on
   - Understand the high-level goal

2. **Deep Dive Questions** - Ask targeted questions to uncover:
   - **Purpose**: Why is this needed? What problem does it solve?
   - **Scope**: What's included? What's explicitly out of scope?
   - **Requirements**: 
     * Functional requirements (what it must do)
     * Non-functional requirements (performance, security, etc.)
     * User stories or use cases
   - **Technical Details**:
     * Technology stack preferences
     * Architecture constraints
     * Integration points
     * Data models
   - **Success Criteria**: How will we know it's done and working?
   - **Timeline & Priority**: Any deadlines or priority tasks?

3. **Clarify and Confirm**
   - Summarize what you've learned
   - Ask for confirmation and corrections
   - Fill in any gaps

4. **Create Specification**
   - Use the SpecificationWriterTool to create a detailed markdown specification
   - Include all gathered information in a structured format
   - The specification will be used by the wyvern to break down into tasks

## Specification Format:

Your specifications should be comprehensive markdown documents with:
- Project/Task Name
- Overview and Purpose
- Detailed Requirements (functional & non-functional)
- Technical Architecture
- User Stories / Use Cases
- Success Criteria
- Out of Scope
- Notes and Considerations

## Style:
- Be conversational and friendly
- Ask one or a few related questions at a time (don't overwhelm)
- Use examples to clarify when needed
- Be thorough but efficient
- Show enthusiasm for their project

## Tools Available:
- **SpecificationWriterTool**: Call this when you have enough information to write the specification
  - Parameters: filename, content (markdown)
  - This saves the specification to the specifications directory

Remember: You're not implementing the project, you're gathering requirements and creating a clear specification that the KoboldTown wyvern and worker agents will use to build it.";
        }

        /// <summary>
        /// Starts an interactive session with the user for requirements gathering
        /// </summary>
        /// <param name="initialMessage">Optional initial message from user</param>
        /// <returns>Dragon's response</returns>
        public async Task<string> StartSessionAsync(string? initialMessage = null)
        {
            if (string.IsNullOrEmpty(initialMessage))
            {
                initialMessage = "I'd like to start a new project.";
            }

            var messages = await RunAsync(initialMessage, maxIterations: 1);
            
            // Return the last assistant message
            var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
            return lastMessage?.Content?.ToString() ?? "Hello! I'm Dragon 🐉. What project would you like to work on?";
        }

        /// <summary>
        /// Continues the conversation with user input
        /// </summary>
        /// <param name="userMessage">User's message</param>
        /// <returns>Dragon's response</returns>
        public async Task<string> ContinueSessionAsync(string userMessage)
        {
            var messages = await RunAsync(userMessage, maxIterations: 1);
            
            var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
            return lastMessage?.Content?.ToString() ?? "I understand. Please continue...";
        }

        /// <summary>
        /// Gets the path where specifications are stored
        /// </summary>
        public string SpecificationsPath => _specificationsPath;
    }

    /// <summary>
    /// Tool for writing specification files
    /// </summary>
    public class SpecificationWriterTool : Tool
    {
        private readonly string _specificationsPath;

        public SpecificationWriterTool(string specificationsPath)
        {
            _specificationsPath = specificationsPath;
        }

        public override string Name => "write_specification";

        public override string Description => 
            "Writes a project or task specification to a markdown file in the specifications directory. " +
            "Use this when you have gathered enough information from the user.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                filename = new
                {
                    type = "string",
                    description = "Filename for the specification (e.g., 'web-app-project.md', 'api-refactor-task.md'). Use kebab-case."
                },
                content = new
                {
                    type = "string",
                    description = "Full markdown content of the specification document"
                }
            },
            required = new[] { "filename", "content" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("filename", out var filenameObj) || 
                !input.TryGetValue("content", out var contentObj))
            {
                return "Error: Both filename and content are required";
            }

            var filename = filenameObj.ToString() ?? "";
            var content = contentObj.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(filename))
            {
                return "Error: Filename cannot be empty";
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return "Error: Content cannot be empty";
            }

            // Ensure .md extension
            if (!filename.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".md";
            }

            // Sanitize filename
            var invalidChars = Path.GetInvalidFileNameChars();
            filename = string.Join("_", filename.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            var fullPath = Path.Combine(_specificationsPath, filename);

            try
            {
                File.WriteAllText(fullPath, content);
                
                SendMessage("success", $"Specification saved: {filename}");
                
                return $"✅ Specification saved successfully to: {fullPath}\n\n" +
                       $"This specification can now be used by the wyvern to break down the project into tasks.\n" +
                       $"File size: {content.Length} characters";
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Failed to write specification: {ex.Message}");
                return $"❌ Error writing specification: {ex.Message}";
            }
        }
    }
}
