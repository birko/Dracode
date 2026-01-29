using DraCode.Agent.Tools;
using DraCode.KoboldLair.Server.Models;

namespace DraCode.KoboldLair.Server.Agents.Tools
{
    /// <summary>
    /// Tool for managing specifications (create, update, load, list)
    /// </summary>
    public class SpecificationManagementTool : Tool
    {
        private readonly string _specificationsPath;
        private readonly Dictionary<string, Specification> _specifications;
        private readonly Action<string>? _onSpecificationUpdated;

        public SpecificationManagementTool(
            string specificationsPath,
            Dictionary<string, Specification> specifications,
            Action<string>? onSpecificationUpdated = null)
        {
            _specificationsPath = specificationsPath;
            _specifications = specifications;
            _onSpecificationUpdated = onSpecificationUpdated;
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

                // Notify that specification was updated (triggers Wyvern reprocessing)
                _onSpecificationUpdated?.Invoke(fullPath);

                SendMessage("success", $"Specification updated: {name}");
                return $"✅ Specification '{name}' updated successfully (version {spec.Version}). Wyvern will reprocess changes.";
            }
            catch (Exception ex)
            {
                return $"Error updating specification: {ex.Message}";
            }
        }
    }
}
