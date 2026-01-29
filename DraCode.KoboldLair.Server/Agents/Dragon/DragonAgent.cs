using System.Text.Json;
using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Server.Models;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Server.Agents.Dragon
{
    /// <summary>
    /// DragonAgent is a specialized agent for gathering project/task requirements from users.
    /// It conducts interactive discussions to understand what needs to be built and produces
    /// detailed specifications that can be used by the wyvern and other KoboldLair agents.
    /// </summary>
    public class DragonAgent : AgentBase
    {
        private readonly string _specificationsPath;
        private readonly Dictionary<string, Specification> _specifications = new();

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
            _specificationsPath = specificationsPath ?? "./specifications";

            // Ensure specifications directory exists
            try
            {
                if (!Directory.Exists(_specificationsPath))
                {
                    Directory.CreateDirectory(_specificationsPath);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - the tool methods will handle missing path gracefully
                Console.WriteLine($"Warning: Could not create specifications directory '{_specificationsPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the tools available to Dragon, including specification and feature management
        /// </summary>
        protected override List<Tool> CreateTools()
        {
            var tools = base.CreateTools();
            tools.Add(new SpecificationManagementTool(_specificationsPath, _specifications));
            tools.Add(new FeatureManagementTool(_specifications));
            return tools;
        }

        /// <summary>
        /// Gets the specialized system prompt for the Dragon agent
        /// </summary>
        private string GetDragonSystemPrompt()
        {
            return @"You are Dragon 🐉, a senior requirements analyst and project architect for KoboldLair.

Your role is to have an interactive conversation with the user to deeply understand their project requirements, then create or update specifications and manage features.

## Your Workflow:

1. **Check for Existing Specification**
   - First, use manage_specification with action:'list' to see what already exists
   - If working on an existing project, use manage_specification with action:'load' to get the current state
   - You can update existing specifications or create new ones

2. **Understand Requirements**
   - Ask what project or feature they want to work on
   - Understand the high-level goal and context
   - Ask targeted questions about purpose, scope, requirements, technical details, success criteria

3. **Manage Features**
   - Create features for new functionality using manage_feature with action:'create'
   - Update features ONLY if they have status ""New"" using manage_feature with action:'update'
   - If a feature is already assigned to Wyrm or in progress, create a NEW feature instead
   - List features to see current status using manage_feature with action:'list'

4. **Create or Update Specification**
   - Use manage_specification with action:'create' for new projects
   - Use manage_specification with action:'update' for existing projects
   - Include comprehensive details: overview, requirements, architecture, success criteria
   - The specification provides context for all features

## Feature Status Lifecycle:
- **New**: Just created by Dragon, can be updated by Dragon
- **AssignedToWyvern**: Wyrm has taken ownership, Dragon cannot modify (create new feature instead)
- **InProgress**: Being worked on by Kobolds
- **Completed**: Implementation finished

## Tools Available:
- **manage_specification**: Manage specifications (actions: list, load, create, update)
- **manage_feature**: Manage features (actions: list, create, update)

## Style:
- Be conversational and friendly
- Check existing state before creating duplicates
- Guide users through the feature workflow
- Be thorough but efficient

Remember: You manage specifications and features. Wyrm reads new features and creates tasks for Kobolds.";
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
            if (lastMessage?.Content == null)
            {
                return "Hello! I'm Dragon 🐉. What project would you like to work on?";
            }

            return ExtractTextFromContent(lastMessage.Content);
        }

        /// <summary>
        /// Continues the conversation with user input
        /// </summary>
        /// <param name="userMessage">User's message</param>
        /// <returns>Dragon's response</returns>
        public async Task<string> ContinueSessionAsync(string userMessage)
        {
            var messages = await RunAsync(userMessage, maxIterations: 10);

            var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
            if (lastMessage?.Content == null)
            {
                return "I understand. Please continue...";
            }

            return ExtractTextFromContent(lastMessage.Content);
        }

        /// <summary>
        /// Extracts text from message content (handles string, ContentBlock, or List<ContentBlock>)
        /// </summary>
        private string ExtractTextFromContent(object content)
        {
            if (content is string text)
            {
                return text;
            }
            else if (content is ContentBlock block)
            {
                return block.Text ?? "";
            }
            else if (content is IEnumerable<ContentBlock> blocks)
            {
                return string.Join("\n", blocks
                    .Where(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
                    .Select(b => b.Text));
            }

            return content?.ToString() ?? "";
        }

        /// <summary>
        /// Gets the path where specifications are stored
        /// </summary>
        public string SpecificationsPath => _specificationsPath;

        /// <summary>
        /// Gets a specification by name
        /// </summary>
        public Specification? GetSpecification(string name)
        {
            return _specifications.TryGetValue(name, out var spec) ? spec : null;
        }

        /// <summary>
        /// Gets all specifications
        /// </summary>
        public IReadOnlyDictionary<string, Specification> GetAllSpecifications() => _specifications;
    }

    /// <summary>
    /// Tool for managing specifications (create, update, load, list)
    /// </summary>
    public class SpecificationManagementTool : Tool
    {
        private readonly string _specificationsPath;
        private readonly Dictionary<string, Specification> _specifications;

        public SpecificationManagementTool(string specificationsPath, Dictionary<string, Specification> specifications)
        {
            _specificationsPath = specificationsPath;
            _specifications = specifications;
        }

        public override string Name => "manage_specification";

        public override string Description =>
            "Manages project specifications: list all, load existing, create new, or update existing specifications.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'list', 'load', 'create', or 'update'",
                    @enum = new[] { "list", "load", "create", "update" }
                },
                name = new
                {
                    type = "string",
                    description = "Specification name (required for load, create, update)"
                },
                content = new
                {
                    type = "string",
                    description = "Markdown content (required for create and update)"
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("action", out var actionObj))
            {
                return "Error: action is required";
            }

            var action = actionObj.ToString()?.ToLower();

            switch (action)
            {
                case "list":
                    return ListSpecifications();
                case "load":
                    return LoadSpecification(input);
                case "create":
                    return CreateSpecification(input);
                case "update":
                    return UpdateSpecification(input);
                default:
                    return $"Error: Unknown action '{action}'";
            }
        }

        private string ListSpecifications()
        {
            if (string.IsNullOrEmpty(_specificationsPath))
            {
                return "Error: Specifications path is not configured.";
            }

            if (!Directory.Exists(_specificationsPath))
            {
                return "No specifications found.";
            }

            var files = Directory.GetFiles(_specificationsPath, "*.md");
            if (files.Length == 0)
            {
                return "No specifications found.";
            }

            var list = string.Join("\n", files.Select(f => $"- {Path.GetFileNameWithoutExtension(f)}"));
            return $"Specifications:\n{list}";
        }

        private string LoadSpecification(Dictionary<string, object> input)
        {
            if (string.IsNullOrEmpty(_specificationsPath))
            {
                return "Error: Specifications path is not configured.";
            }

            if (!input.TryGetValue("name", out var nameObj))
            {
                return "Error: name is required for load action";
            }

            var name = nameObj.ToString() ?? "";
            var filename = name.EndsWith(".md") ? name : $"{name}.md";
            var fullPath = Path.Combine(_specificationsPath, filename);

            if (!File.Exists(fullPath))
            {
                return $"Error: Specification '{name}' not found";
            }

            try
            {
                var content = File.ReadAllText(fullPath);
                var spec = _specifications.GetValueOrDefault(name) ?? new Specification
                {
                    Name = name,
                    FilePath = fullPath,
                    Content = content
                };

                spec.Content = content;
                _specifications[name] = spec;

                return $"✅ Loaded specification '{name}':\n\n{content}\n\nFeatures: {spec.Features.Count}";
            }
            catch (Exception ex)
            {
                return $"Error loading specification: {ex.Message}";
            }
        }

        private string CreateSpecification(Dictionary<string, object> input)
        {
            if (string.IsNullOrEmpty(_specificationsPath))
            {
                return "Error: Specifications path is not configured.";
            }

            if (!input.TryGetValue("name", out var nameObj) || !input.TryGetValue("content", out var contentObj))
            {
                return "Error: name and content are required for create action";
            }

            var name = nameObj.ToString() ?? "";
            var content = contentObj.ToString() ?? "";
            var filename = name.EndsWith(".md") ? name : $"{name}.md";
            var fullPath = Path.Combine(_specificationsPath, filename);

            if (File.Exists(fullPath))
            {
                return $"Error: Specification '{name}' already exists. Use 'update' action instead.";
            }

            try
            {
                File.WriteAllText(fullPath, content);

                var spec = new Specification
                {
                    Name = name,
                    FilePath = fullPath,
                    Content = content,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _specifications[name] = spec;

                SendMessage("success", $"Specification created: {name}");
                return $"✅ Specification '{name}' created successfully at: {fullPath}";
            }
            catch (Exception ex)
            {
                return $"Error creating specification: {ex.Message}";
            }
        }

        private string UpdateSpecification(Dictionary<string, object> input)
        {
            if (string.IsNullOrEmpty(_specificationsPath))
            {
                return "Error: Specifications path is not configured.";
            }

            if (!input.TryGetValue("name", out var nameObj) || !input.TryGetValue("content", out var contentObj))
            {
                return "Error: name and content are required for update action";
            }

            var name = nameObj.ToString() ?? "";
            var content = contentObj.ToString() ?? "";
            var filename = name.EndsWith(".md") ? name : $"{name}.md";
            var fullPath = Path.Combine(_specificationsPath, filename);

            if (!File.Exists(fullPath))
            {
                return $"Error: Specification '{name}' does not exist. Use 'create' action instead.";
            }

            try
            {
                File.WriteAllText(fullPath, content);

                var spec = _specifications.GetValueOrDefault(name) ?? new Specification
                {
                    Name = name,
                    FilePath = fullPath
                };

                spec.Content = content;
                spec.UpdatedAt = DateTime.UtcNow;
                spec.Version++;

                _specifications[name] = spec;

                SendMessage("success", $"Specification updated: {name}");
                return $"✅ Specification '{name}' updated successfully (version {spec.Version})";
            }
            catch (Exception ex)
            {
                return $"Error updating specification: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Tool for managing features within specifications
    /// </summary>
    public class FeatureManagementTool : Tool
    {
        private readonly Dictionary<string, Specification> _specifications;

        public FeatureManagementTool(Dictionary<string, Specification> specifications)
        {
            _specifications = specifications;
        }

        public override string Name => "manage_feature";

        public override string Description =>
            "Manages features within specifications: create, update (only if status is New), or list features.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'create', 'update', or 'list'",
                    @enum = new[] { "create", "update", "list" }
                },
                specification_name = new
                {
                    type = "string",
                    description = "Name of the specification (required for all actions)"
                },
                feature_name = new
                {
                    type = "string",
                    description = "Feature name (required for create and update)"
                },
                description = new
                {
                    type = "string",
                    description = "Feature description (required for create, optional for update)"
                },
                priority = new
                {
                    type = "string",
                    description = "Feature priority: low, medium, high, critical (optional, default: medium)"
                }
            },
            required = new[] { "action", "specification_name" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("action", out var actionObj) || !input.TryGetValue("specification_name", out var specNameObj))
            {
                return "Error: action and specification_name are required";
            }

            var action = actionObj.ToString()?.ToLower();
            var specName = specNameObj.ToString() ?? "";

            if (!_specifications.TryGetValue(specName, out var spec))
            {
                return $"Error: Specification '{specName}' not found. Load or create it first.";
            }

            switch (action)
            {
                case "create":
                    return CreateFeature(spec, input);
                case "update":
                    return UpdateFeature(spec, input);
                case "list":
                    return ListFeatures(spec);
                default:
                    return $"Error: Unknown action '{action}'";
            }
        }

        private string CreateFeature(Specification spec, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("feature_name", out var nameObj) || !input.TryGetValue("description", out var descObj))
            {
                return "Error: feature_name and description are required for create action";
            }

            var name = nameObj.ToString() ?? "";
            var description = descObj.ToString() ?? "";
            var priority = input.TryGetValue("priority", out var prioObj) ? prioObj.ToString() ?? "medium" : "medium";

            var feature = new Feature
            {
                Name = name,
                Description = description,
                Priority = priority,
                SpecificationId = spec.Id,
                Status = FeatureStatus.New
            };

            spec.Features.Add(feature);
            spec.UpdatedAt = DateTime.UtcNow;

            SendMessage("success", $"Feature created: {name}");
            return $"✅ Feature '{name}' created with status 'New'\nID: {feature.Id}\nPriority: {priority}";
        }

        private string UpdateFeature(Specification spec, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("feature_name", out var nameObj))
            {
                return "Error: feature_name is required for update action";
            }

            var name = nameObj.ToString() ?? "";
            var feature = spec.Features.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (feature == null)
            {
                return $"Error: Feature '{name}' not found in specification '{spec.Name}'";
            }

            if (feature.Status != FeatureStatus.New)
            {
                return $"❌ Cannot update feature '{name}': Status is '{feature.Status}'. Only features with status 'New' can be updated.\n" +
                       $"Create a new feature instead if you need to add more functionality.";
            }

            if (input.TryGetValue("description", out var descObj))
            {
                feature.Description = descObj.ToString() ?? feature.Description;
            }

            if (input.TryGetValue("priority", out var prioObj))
            {
                feature.Priority = prioObj.ToString() ?? feature.Priority;
            }

            feature.UpdatedAt = DateTime.UtcNow;
            spec.UpdatedAt = DateTime.UtcNow;

            SendMessage("success", $"Feature updated: {name}");
            return $"✅ Feature '{name}' updated successfully\nStatus: {feature.Status}\nPriority: {feature.Priority}";
        }

        private string ListFeatures(Specification spec)
        {
            if (spec.Features.Count == 0)
            {
                return $"No features found in specification '{spec.Name}'";
            }

            var grouped = spec.Features.GroupBy(f => f.Status);
            var result = new System.Text.StringBuilder();
            result.AppendLine($"Features in '{spec.Name}':\n");

            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                result.AppendLine($"## {group.Key} ({group.Count()})");
                foreach (var feature in group)
                {
                    result.AppendLine($"- **{feature.Name}** (Priority: {feature.Priority})");
                    result.AppendLine($"  {feature.Description}");
                    result.AppendLine($"  ID: {feature.Id}");
                }
                result.AppendLine();
            }

            return result.ToString();
        }
    }

    /// <summary>
    /// Legacy tool for backward compatibility - redirects to SpecificationManagementTool
    /// </summary>
    public class SpecificationWriterTool : Tool
    {
        private readonly SpecificationManagementTool _managementTool;

        public SpecificationWriterTool(string specificationsPath, Dictionary<string, Specification> specifications)
        {
            _managementTool = new SpecificationManagementTool(specificationsPath, specifications);
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
            var name = Path.GetFileNameWithoutExtension(filename);

            // Redirect to create action
            var managementInput = new Dictionary<string, object>
            {
                { "action", "create" },
                { "name", name },
                { "content", contentObj }
            };

            return _managementTool.Execute(workingDirectory, managementInput);
        }
    }
}
