using System.Text.Json;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Agents.Tools
{
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
            "Manages features within specifications: create, update (only if status is Draft), or list features. " +
            "Features are created as 'Draft' and must be marked as 'Ready' using process_features before Wyvern will process them.";

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

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
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
                    return await CreateFeatureAsync(spec, input);
                case "update":
                    return await UpdateFeatureAsync(spec, input);
                case "list":
                    return ListFeatures(spec);
                default:
                    return $"Error: Unknown action '{action}'";
            }
        }

        private async Task<string> CreateFeatureAsync(Specification spec, Dictionary<string, object> input)
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
                Status = FeatureStatus.Draft
            };

            spec.WithFeatures(features =>
            {
                features.Add(feature);
                spec.UpdatedAt = DateTime.UtcNow;
            });

            await SaveFeaturesAsync(spec);

            SendMessage("success", $"Feature created: {name}");
            return $"✅ Feature '{name}' created with status 'Draft'\nID: {feature.Id}\nPriority: {priority}\n\n" +
                   $"**⚠️ Next Step:** Use `process_features` to mark this feature as 'Ready' when you want it to be processed. " +
                   $"Only 'Ready' features will be included when you update the specification.";
        }

        private async Task<string> UpdateFeatureAsync(Specification spec, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("feature_name", out var nameObj))
            {
                return "Error: feature_name is required for update action";
            }

            var name = nameObj.ToString() ?? "";

            string? result = null;
            spec.WithFeatures(features =>
            {
                var feature = features.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (feature == null)
                {
                    result = $"Error: Feature '{name}' not found in specification '{spec.Name}'";
                    return;
                }

                // Allow updating Draft features only
                if (feature.Status != FeatureStatus.Draft)
                {
                    result = $"❌ Cannot update feature '{name}': Status is '{feature.Status}'. Only features with status 'Draft' can be updated.\n" +
                           $"Create a new feature instead if you need to add more functionality.";
                    return;
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
            });
            
            if (result != null)
                return result;

            await SaveFeaturesAsync(spec);

            SendMessage("success", $"Feature updated: {name}");
            return $"✅ Feature '{name}' updated successfully";
        }

        private string ListFeatures(Specification spec)
        {
            var features = spec.GetFeaturesCopy();

            if (features.Count == 0)
            {
                return $"No features in '{spec.Name}'";
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine($"**{features.Count} feature(s) in '{spec.Name}':**\n");
            result.AppendLine("| Status | Priority | Feature | Description |");
            result.AppendLine("|--------|----------|---------|-------------|");

            foreach (var feature in features.OrderBy(f => f.Status).ThenBy(f => f.Priority))
            {
                var displayStatus = feature.Status;

                var statusIcon = displayStatus switch
                {
                    Models.Tasks.FeatureStatus.Draft => "📝",
                    Models.Tasks.FeatureStatus.Ready => "✅",
                    Models.Tasks.FeatureStatus.AssignedToWyvern => "📋",
                    Models.Tasks.FeatureStatus.InProgress => "🔨",
                    Models.Tasks.FeatureStatus.Completed => "🎉",
                    _ => "❓"
                };
                var desc = feature.Description.Length > 50 ? feature.Description[..47] + "..." : feature.Description;
                result.AppendLine($"| {statusIcon} {displayStatus} | {feature.Priority} | {feature.Name} | {desc} |");
            }

            return result.ToString();
        }

        /// <summary>
        /// Saves features to a JSON file in the project folder.
        /// Uses consolidated structure: {projectFolder}/specification.features.json
        /// </summary>
        private async Task SaveFeaturesAsync(Specification spec)
        {
            if (string.IsNullOrEmpty(spec.Name))
                return;

            // Determine the folder to save features to
            var folder = spec.ProjectFolder;
            if (string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(spec.FilePath))
            {
                folder = Path.GetDirectoryName(spec.FilePath);
            }

            if (string.IsNullOrEmpty(folder))
                return;

            try
            {
                // Use consolidated naming: specification.features.json (no project name prefix)
                var featuresPath = Path.Combine(folder, "specification.features.json");
                var options = new JsonSerializerOptions { WriteIndented = true };

                // Create wrapper object with version metadata
                var featuresData = new
                {
                    specificationVersion = spec.Version,
                    specificationContentHash = spec.ContentHash,
                    features = spec.Features
                };
                var json = JsonSerializer.Serialize(featuresData, options);
                await File.WriteAllTextAsync(featuresPath, json);
            }
            catch (Exception ex)
            {
                SendMessage("warning", $"Could not save features: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads features from JSON file if it exists.
        /// Uses consolidated structure: {projectFolder}/specification.features.json
        /// Handles both wrapped format (with version) and legacy format (features only).
        /// </summary>
        /// <param name="spec">The specification to load features into</param>
        /// <param name="folderPath">The project folder path</param>
        public static async Task LoadFeaturesAsync(Specification spec, string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            try
            {
                var featuresPath = Path.Combine(folderPath, "specification.features.json");

                if (File.Exists(featuresPath))
                {
                    var json = await File.ReadAllTextAsync(featuresPath);

                    // Try new wrapped format first
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("features", out var featuresProp))
                    {
                        var features = JsonSerializer.Deserialize<List<Feature>>(featuresProp.GetRawText());
                        if (features != null)
                        {
                            spec.Features = features;
                        }
                        // Also read version if available
                        if (doc.RootElement.TryGetProperty("specificationVersion", out var versionProp))
                        {
                            spec.Version = versionProp.GetInt32();
                        }
                        // Also read content hash if available
                        if (doc.RootElement.TryGetProperty("specificationContentHash", out var hashProp))
                        {
                            spec.ContentHash = hashProp.GetString() ?? string.Empty;
                        }
                    }
                    else
                    {
                        // Old format - direct array
                        var features = JsonSerializer.Deserialize<List<Feature>>(json);
                        if (features != null)
                        {
                            spec.Features = features;
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore load errors - features will be empty
            }
        }
    }
}
