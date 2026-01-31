using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for managing specifications (create, update, load, list)
    /// </summary>
    public class SpecificationManagementTool : Tool
    {
        private readonly string _specificationsPath;
        private readonly string _projectsPath;
        private readonly Dictionary<string, Specification> _specifications;
        private readonly Action<string>? _onSpecificationUpdated;
        private readonly Func<string, string>? _getProjectFolder;

        public SpecificationManagementTool(
            string specificationsPath,
            Dictionary<string, Specification> specifications,
            Action<string>? onSpecificationUpdated = null,
            Func<string, string>? getProjectFolder = null,
            string projectsPath = "./projects")
        {
            _specificationsPath = specificationsPath;
            _specifications = specifications;
            _onSpecificationUpdated = onSpecificationUpdated;
            _getProjectFolder = getProjectFolder;
            _projectsPath = projectsPath;
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
            if (!input.TryGetValue("name", out var nameObj))
            {
                return "Error: name is required for load action";
            }

            var name = nameObj.ToString() ?? "";

            // First check if we have the spec cached with its project folder
            if (_specifications.TryGetValue(name, out var existingSpec) && !string.IsNullOrEmpty(existingSpec.FilePath))
            {
                if (File.Exists(existingSpec.FilePath))
                {
                    var content = File.ReadAllText(existingSpec.FilePath);
                    existingSpec.Content = content;

                    // Load features from project folder
                    var projectFolder = existingSpec.ProjectFolder ?? Path.GetDirectoryName(existingSpec.FilePath) ?? _specificationsPath;
                    FeatureManagementTool.LoadFeatures(existingSpec, projectFolder);

                    return $"✅ Loaded specification '{name}':\n\n{content}\n\nFeatures: {existingSpec.Features.Count}";
                }
            }

            // Try consolidated structure first: {projectsPath}/{name}/specification.md
            var projectFolder2 = Path.Combine(_projectsPath, SanitizeProjectName(name));
            var consolidatedPath = Path.Combine(projectFolder2, "specification.md");

            string? fullPath = null;
            string? folder = null;

            if (File.Exists(consolidatedPath))
            {
                fullPath = consolidatedPath;
                folder = projectFolder2;
            }
            else if (!string.IsNullOrEmpty(_specificationsPath))
            {
                // Fallback to legacy structure
                var filename = name.EndsWith(".md") ? name : $"{name}.md";
                var legacyPath = Path.Combine(_specificationsPath, filename);
                if (File.Exists(legacyPath))
                {
                    fullPath = legacyPath;
                    folder = _specificationsPath;
                }
            }

            if (fullPath == null)
            {
                return $"Error: Specification '{name}' not found";
            }

            try
            {
                var content = File.ReadAllText(fullPath);
                var spec = new Specification
                {
                    Name = name,
                    FilePath = fullPath,
                    ProjectFolder = folder ?? "",
                    Content = content
                };

                _specifications[name] = spec;

                // Load features from project folder
                if (!string.IsNullOrEmpty(folder))
                {
                    FeatureManagementTool.LoadFeatures(spec, folder);
                }

                return $"✅ Loaded specification '{name}':\n\n{content}\n\nFeatures: {spec.Features.Count}";
            }
            catch (Exception ex)
            {
                return $"Error loading specification: {ex.Message}";
            }
        }

        /// <summary>
        /// Sanitizes project name for use as directory name
        /// </summary>
        private static string SanitizeProjectName(string projectName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", projectName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim().Replace(" ", "-").ToLowerInvariant();
        }

        private string CreateSpecification(Dictionary<string, object> input)
        {
            if (!input.TryGetValue("name", out var nameObj) || !input.TryGetValue("content", out var contentObj))
            {
                return "Error: name and content are required for create action";
            }

            var name = nameObj.ToString() ?? "";
            var content = contentObj.ToString() ?? "";

            string fullPath;
            string projectFolder;

            // Use callback to get project folder if available (consolidated structure)
            if (_getProjectFolder != null)
            {
                projectFolder = _getProjectFolder(name);
                fullPath = Path.Combine(projectFolder, "specification.md");
            }
            else if (!string.IsNullOrEmpty(_specificationsPath))
            {
                // Fallback to legacy structure
                projectFolder = _specificationsPath;
                var filename = name.EndsWith(".md") ? name : $"{name}.md";
                fullPath = Path.Combine(_specificationsPath, filename);
            }
            else
            {
                return "Error: No project folder callback or specifications path configured.";
            }

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
                    ProjectFolder = projectFolder,
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
            if (!input.TryGetValue("name", out var nameObj) || !input.TryGetValue("content", out var contentObj))
            {
                return "Error: name and content are required for update action";
            }

            var name = nameObj.ToString() ?? "";
            var content = contentObj.ToString() ?? "";

            // Get existing spec to find its path
            string? fullPath = null;
            string? folder = null;

            if (_specifications.TryGetValue(name, out var existingSpec) && !string.IsNullOrEmpty(existingSpec.FilePath))
            {
                fullPath = existingSpec.FilePath;
                folder = existingSpec.ProjectFolder ?? Path.GetDirectoryName(existingSpec.FilePath);
            }
            else
            {
                // Try consolidated structure
                var projectFolder = Path.Combine(_projectsPath, SanitizeProjectName(name));
                var consolidatedPath = Path.Combine(projectFolder, "specification.md");

                if (File.Exists(consolidatedPath))
                {
                    fullPath = consolidatedPath;
                    folder = projectFolder;
                }
                else if (!string.IsNullOrEmpty(_specificationsPath))
                {
                    // Fallback to legacy structure
                    var filename = name.EndsWith(".md") ? name : $"{name}.md";
                    var legacyPath = Path.Combine(_specificationsPath, filename);
                    if (File.Exists(legacyPath))
                    {
                        fullPath = legacyPath;
                        folder = _specificationsPath;
                    }
                }
            }

            if (fullPath == null || !File.Exists(fullPath))
            {
                return $"Error: Specification '{name}' does not exist. Use 'create' action instead.";
            }

            try
            {
                File.WriteAllText(fullPath, content);

                var spec = _specifications.GetValueOrDefault(name) ?? new Specification
                {
                    Name = name,
                    FilePath = fullPath,
                    ProjectFolder = folder ?? ""
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
