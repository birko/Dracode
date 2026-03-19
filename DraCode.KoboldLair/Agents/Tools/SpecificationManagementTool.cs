using Birko.Validation;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services.EventSourcing;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for managing specifications (create, update, load, list)
    /// </summary>
    public class SpecificationManagementTool : Tool
    {
        private readonly string _projectsPath;
        private readonly Dictionary<string, Specification> _specifications;
        private readonly object _specificationsLock = new object();
        private readonly Action<string>? _onSpecificationUpdated;
        private readonly Func<string, string>? _getProjectFolder;
        private readonly Func<string, Task<string?>>? _onProjectLoaded;
        private readonly IValidator<Specification>? _specificationValidator;
        private readonly SpecificationEventService? _eventService;

        public SpecificationManagementTool(
            Dictionary<string, Specification> specifications,
            Action<string>? onSpecificationUpdated = null,
            Func<string, string>? getProjectFolder = null,
            string? projectsPath = "./projects",
            Func<string, Task<string?>>? onProjectLoaded = null,
            IValidator<Specification>? specificationValidator = null,
            SpecificationEventService? eventService = null)
        {
            _specifications = specifications;
            _onSpecificationUpdated = onSpecificationUpdated;
            _getProjectFolder = getProjectFolder;
            _projectsPath = projectsPath ?? "./projects";
            _onProjectLoaded = onProjectLoaded;
            _specificationValidator = specificationValidator;
            _eventService = eventService;
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

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
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
                    return await LoadSpecificationAsync(input);
                case "create":
                    return await CreateSpecificationAsync(input);
                case "update":
                    return await UpdateSpecificationAsync(input);
                default:
                    return $"Error: Unknown action '{action}'";
            }
        }

        private string ListSpecifications()
        {
            if (string.IsNullOrEmpty(_projectsPath))
            {
                return "Error: Projects path is not configured.";
            }

            if (!Directory.Exists(_projectsPath))
            {
                return "No specifications found.";
            }

            // List projects that have specification.md in their folder
            var projectFolders = Directory.GetDirectories(_projectsPath)
                .Where(dir => File.Exists(Path.Combine(dir, "specification.md")))
                .Select(dir => Path.GetFileName(dir))
                .ToList();

            if (projectFolders.Count == 0)
            {
                return "No specifications found.";
            }

            var list = string.Join("\n", projectFolders.Select(f => $"- {f}"));
            return $"Specifications:\n{list}";
        }

        private async Task<string> LoadSpecificationAsync(Dictionary<string, object> input)
        {
            if (!input.TryGetValue("name", out var nameObj))
            {
                return "Error: name is required for load action";
            }

            var name = nameObj.ToString() ?? "";

            // First check if we have the spec cached with its project folder (under lock)
            Specification? existingSpec = null;
            string? existingFilePath = null;
            lock (_specificationsLock)
            {
                if (_specifications.TryGetValue(name, out existingSpec) && !string.IsNullOrEmpty(existingSpec.FilePath))
                {
                    existingFilePath = existingSpec.FilePath;
                }
            }

            if (existingSpec != null && existingFilePath != null && File.Exists(existingFilePath))
            {
                var content = await File.ReadAllTextAsync(existingFilePath);
                existingSpec.Content = content;

                // Load features from project folder
                var projectFolder = existingSpec.ProjectFolder ?? Path.GetDirectoryName(existingFilePath) ?? "";
                if (!string.IsNullOrEmpty(projectFolder))
                {
                    await FeatureManagementTool.LoadFeaturesAsync(existingSpec, projectFolder);
                }

                var result = $"✅ Loaded specification '{name}':\n\n{content}\n\nFeatures: {existingSpec.Features.Count}";
                if (!string.IsNullOrEmpty(projectFolder) && _onProjectLoaded != null)
                {
                    var summary = await _onProjectLoaded(projectFolder);
                    if (!string.IsNullOrEmpty(summary))
                        result += $"\n\n{summary}";
                }
                return result;
            }

            // Try consolidated structure: {projectsPath}/{name}/specification.md
            var projectFolder2 = Path.Combine(_projectsPath, SanitizeProjectName(name));
            var specPath = Path.Combine(projectFolder2, "specification.md");

            if (!File.Exists(specPath))
            {
                return $"Error: Specification '{name}' not found";
            }

            try
            {
                var content = await File.ReadAllTextAsync(specPath);
                var spec = new Specification
                {
                    Name = name,
                    FilePath = specPath,
                    ProjectFolder = projectFolder2,
                    Content = content
                };

                lock (_specificationsLock)
                {
                    _specifications[name] = spec;
                }

                // Load features from project folder
                await FeatureManagementTool.LoadFeaturesAsync(spec, projectFolder2);

                var result = $"✅ Loaded specification '{name}':\n\n{content}\n\nFeatures: {spec.Features.Count}";
                if (_onProjectLoaded != null)
                {
                    var summary = await _onProjectLoaded(projectFolder2);
                    if (!string.IsNullOrEmpty(summary))
                        result += $"\n\n{summary}";
                }
                return result;
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

        private async Task<string> CreateSpecificationAsync(Dictionary<string, object> input)
        {
            if (!input.TryGetValue("name", out var nameObj) || !input.TryGetValue("content", out var contentObj))
            {
                return "Error: name and content are required for create action";
            }

            var name = nameObj.ToString() ?? "";
            var content = contentObj.ToString() ?? "";

            string fullPath;
            string projectFolder;

            // Use callback to get project folder if available
            if (_getProjectFolder != null)
            {
                projectFolder = _getProjectFolder(name);
                fullPath = Path.Combine(projectFolder, "specification.md");
            }
            else
            {
                // Create project folder directly in projectsPath
                projectFolder = Path.Combine(_projectsPath, SanitizeProjectName(name));
                if (!Directory.Exists(projectFolder))
                {
                    Directory.CreateDirectory(projectFolder);
                }
                fullPath = Path.Combine(projectFolder, "specification.md");
            }

            if (File.Exists(fullPath))
            {
                return $"Error: Specification '{name}' already exists. Use 'update' action instead.";
            }

            try
            {
                var spec = new Specification
                {
                    Name = name,
                    FilePath = fullPath,
                    ProjectFolder = projectFolder,
                    Content = content,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Validate before persisting
                if (_specificationValidator != null)
                {
                    var validation = _specificationValidator.Validate(spec);
                    if (!validation.IsValid)
                    {
                        var errors = string.Join("; ", validation.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
                        return $"Error: Specification validation failed — {errors}";
                    }
                }

                // Write file (outside lock to avoid holding lock during I/O)
                await File.WriteAllTextAsync(fullPath, content);

                // Update dictionary under lock
                lock (_specificationsLock)
                {
                    _specifications[name] = spec;
                }

                // Record event sourcing audit trail
                if (_eventService != null)
                {
                    try { await _eventService.RecordSpecificationCreatedAsync(spec.Id, name, content, spec.ProjectId); }
                    catch { /* Event recording is non-critical */ }
                }

                SendMessage("success", $"Specification created: {name}");
                return $"✅ Specification '{name}' created successfully at: {fullPath}";
            }
            catch (Exception ex)
            {
                return $"Error creating specification: {ex.Message}";
            }
        }

        private async Task<string> UpdateSpecificationAsync(Dictionary<string, object> input)
        {
            if (!input.TryGetValue("name", out var nameObj) || !input.TryGetValue("content", out var contentObj))
            {
                return "Error: name and content are required for update action";
            }

            var name = nameObj.ToString() ?? "";
            var content = contentObj.ToString() ?? "";

            // Get existing spec to find its path (read under lock)
            string? fullPath = null;
            string? folder = null;

            lock (_specificationsLock)
            {
                if (_specifications.TryGetValue(name, out var existingSpec) && !string.IsNullOrEmpty(existingSpec.FilePath))
                {
                    fullPath = existingSpec.FilePath;
                    folder = existingSpec.ProjectFolder ?? Path.GetDirectoryName(existingSpec.FilePath);
                }
            }
            
            if (fullPath == null)
            {
                // Try consolidated structure
                var projectFolder = Path.Combine(_projectsPath, SanitizeProjectName(name));
                var specPath = Path.Combine(projectFolder, "specification.md");

                if (File.Exists(specPath))
                {
                    fullPath = specPath;
                    folder = projectFolder;
                }
            }

            if (fullPath == null || !File.Exists(fullPath))
            {
                return $"Error: Specification '{name}' does not exist. Use 'create' action instead.";
            }

            try
            {
                // Validate before persisting
                if (_specificationValidator != null)
                {
                    var tempSpec = new Specification
                    {
                        Name = name,
                        FilePath = fullPath,
                        Content = content
                    };
                    var validation = _specificationValidator.Validate(tempSpec);
                    if (!validation.IsValid)
                    {
                        var errors = string.Join("; ", validation.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
                        return $"Error: Specification validation failed — {errors}";
                    }
                }

                // Write file (outside lock)
                await File.WriteAllTextAsync(fullPath, content);

                // Update spec under lock
                lock (_specificationsLock)
                {
                    var spec = _specifications.GetValueOrDefault(name) ?? new Specification
                    {
                        Name = name,
                        FilePath = fullPath,
                        ProjectFolder = folder ?? ""
                    };

                    spec.Content = content;
                    spec.UpdatedAt = DateTime.UtcNow;
                    spec.IncrementVersion();

                    _specifications[name] = spec;
                }

                // Record event sourcing audit trail
                if (_eventService != null)
                {
                    string specId;
                    int specVersion;
                    string prevHash;
                    lock (_specificationsLock)
                    {
                        var s = _specifications[name];
                        specId = s.Id;
                        specVersion = s.Version;
                        prevHash = s.ContentHash;
                    }
                    try { await _eventService.RecordSpecificationUpdatedAsync(specId, content, prevHash, specVersion); }
                    catch { /* Event recording is non-critical */ }
                }

                // Notify that specification was updated (triggers Wyvern reprocessing)
                _onSpecificationUpdated?.Invoke(fullPath);

                SendMessage("success", $"Specification updated: {name}");

                int version;
                lock (_specificationsLock)
                {
                    version = _specifications[name].Version;
                }
                return $"✅ Specification '{name}' updated successfully (version {version}). Wyvern will reprocess changes.\n\n" +
                       $"💡 **Tip**: If you want to restart the project from scratch with this new specification, " +
                       $"ask Warden to use `reset_project` to clear all existing analysis, tasks, and workspace.";
            }
            catch (Exception ex)
            {
                return $"Error updating specification: {ex.Message}";
            }
        }
    }
}
