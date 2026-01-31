using System.Text.Json;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Server.Models.Projects;
using DraCode.KoboldLair.Server.Models.Tasks;

namespace DraCode.KoboldLair.Server.Agents.Tools
{
    /// <summary>
    /// Tool for managing features within specifications
    /// </summary>
    public class FeatureManagementTool : Tool
    {
        private readonly Dictionary<string, Specification> _specifications;
        private readonly string? _specificationsPath;

        public FeatureManagementTool(Dictionary<string, Specification> specifications, string? specificationsPath = null)
        {
            _specifications = specifications;
            _specificationsPath = specificationsPath;
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
            SaveFeatures(spec);

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
            SaveFeatures(spec);

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

        /// <summary>
        /// Saves features to a JSON file alongside the specification
        /// </summary>
        private void SaveFeatures(Specification spec)
        {
            if (string.IsNullOrEmpty(_specificationsPath) || string.IsNullOrEmpty(spec.Name))
                return;

            try
            {
                var featuresPath = Path.Combine(_specificationsPath, $"{spec.Name}.features.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(spec.Features, options);
                File.WriteAllText(featuresPath, json);
            }
            catch (Exception ex)
            {
                SendMessage("warning", $"Could not save features: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads features from JSON file if it exists
        /// </summary>
        public static void LoadFeatures(Specification spec, string specificationsPath)
        {
            if (string.IsNullOrEmpty(specificationsPath) || string.IsNullOrEmpty(spec.Name))
                return;

            try
            {
                var featuresPath = Path.Combine(specificationsPath, $"{spec.Name}.features.json");
                if (File.Exists(featuresPath))
                {
                    var json = File.ReadAllText(featuresPath);
                    var features = JsonSerializer.Deserialize<List<Feature>>(json);
                    if (features != null)
                    {
                        spec.Features = features;
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
