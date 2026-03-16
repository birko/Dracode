using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for managing feature processing workflow - promoting Draft features to Ready status.
    /// This enables selective feature processing before triggering Wyvern analysis.
    /// </summary>
    public class ProcessFeaturesTool : Tool
    {
        private readonly Dictionary<string, Specification> _specifications;
        private readonly Action<string>? _onSpecificationUpdated;

        public ProcessFeaturesTool(
            Dictionary<string, Specification> specifications,
            Action<string>? onSpecificationUpdated = null)
        {
            _specifications = specifications;
            _onSpecificationUpdated = onSpecificationUpdated;
        }

        public override string Name => "process_features";

        public override string Description =>
            "Manages feature processing: list draft features, promote selected features to 'Ready', and trigger specification update. " +
            "Features must be 'Ready' before Wyvern will process them. Use action 'list' to see draft features, " +
            "'promote' to mark features as ready, or 'update_spec' to trigger reanalysis.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform",
                    @enum = new[] { "list", "promote", "update_spec" }
                },
                specification_name = new
                {
                    type = "string",
                    description = "Name of the specification (required for all actions)"
                },
                feature_names = new
                {
                    type = "string",
                    description = "Comma-separated list of feature names to promote (required for 'promote' action)"
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
                return $"Error: Specification '{specName}' not found. Load it first.";
            }

            return action switch
            {
                "list" => ListDraftFeatures(spec),
                "promote" => await PromoteFeaturesAsync(spec, input),
                "update_spec" => TriggerSpecificationUpdate(spec),
                _ => $"Error: Unknown action '{action}'"
            };
        }

        private string ListDraftFeatures(Specification spec)
        {
            var features = spec.GetFeaturesCopy();

            var draftFeatures = features
                .Where(f => f.Status == FeatureStatus.Draft)
                .OrderBy(f => f.Priority)
                .ThenBy(f => f.CreatedAt)
                .ToList();

            if (draftFeatures.Count == 0)
            {
                return $"✅ No draft features in '{spec.Name}'. All features are either Ready or already processed.\n\n" +
                       $"Use `manage_feature` with action 'create' to add new features.";
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine($"**{draftFeatures.Count} draft feature(s) in '{spec.Name}':**\n");
            result.AppendLine("These features are NOT ready for processing. Use `process_features` with action 'promote' to mark them as Ready.\n");
            result.AppendLine("| Priority | Feature | Description | Created |");
            result.AppendLine("|----------|---------|-------------|---------|");

            foreach (var feature in draftFeatures)
            {
                var desc = feature.Description.Length > 60 ? feature.Description[..57] + "..." : feature.Description;
                result.AppendLine($"| {feature.Priority} | {feature.Name} | {desc} | {feature.CreatedAt:MM-dd HH:mm} |");
            }

            result.AppendLine();
            result.AppendLine("**To promote features to Ready:**");
            result.AppendLine($"```\nprocess_features action:'promote' specification_name:'{spec.Name}' feature_names:'feature1, feature2'\n```");

            var readyCount = features.Count(f => f.Status == FeatureStatus.Ready);
            if (readyCount > 0)
            {
                result.AppendLine();
                result.AppendLine($"**Note:** There are also {readyCount} feature(s) already marked as Ready. " +
                                $"Use action 'update_spec' to trigger Wyvern analysis for all Ready features.");
            }

            return result.ToString();
        }

        private async Task<string> PromoteFeaturesAsync(Specification spec, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("feature_names", out var namesObj))
            {
                return "Error: feature_names is required for promote action. Use comma-separated names like: 'feature1, feature2'";
            }

            var namesStr = namesObj.ToString() ?? "";
            var names = namesStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .ToList();

            if (names.Count == 0)
            {
                return "Error: No feature names provided. Use comma-separated names like: 'feature1, feature2'";
            }

            var promoted = new List<string>();
            var notFound = new List<string>();
            var notDraft = new List<string>();

            spec.WithFeatures(features =>
            {
                foreach (var name in names)
                {
                    var feature = features.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (feature == null)
                    {
                        notFound.Add(name);
                        continue;
                    }

                    if (feature.Status != FeatureStatus.Draft)
                    {
                        notDraft.Add($"{name} (status: {feature.Status})");
                        continue;
                    }

                    // Promote to Ready
                    feature.Status = FeatureStatus.Ready;
                    feature.UpdatedAt = DateTime.UtcNow;
                    promoted.Add(feature.Name);
                }

                if (promoted.Count > 0)
                {
                    spec.UpdatedAt = DateTime.UtcNow;
                }
            });

            await SaveFeaturesAsync(spec);

            var result = new System.Text.StringBuilder();

            if (promoted.Count > 0)
            {
                result.AppendLine($"✅ **Promoted {promoted.Count} feature(s) to Ready:**");
                foreach (var name in promoted)
                {
                    result.AppendLine($"   - {name}");
                }
                SendMessage("success", $"Promoted {promoted.Count} features to Ready");
            }

            if (notFound.Count > 0)
            {
                result.AppendLine();
                result.AppendLine($"⚠️ **Not found:** {string.Join(", ", notFound)}");
            }

            if (notDraft.Count > 0)
            {
                result.AppendLine();
                result.AppendLine($"⚠️ **Not draft (cannot promote):** {string.Join(", ", notDraft)}");
            }

            if (promoted.Count > 0)
            {
                result.AppendLine();
                result.AppendLine($"**Next Step:** Use `process_features` with action 'update_spec' and specification_name:'{spec.Name}' " +
                                $"to trigger Wyvern analysis for all Ready features.");
            }

            return result.ToString();
        }

        private string TriggerSpecificationUpdate(Specification spec)
        {
            // Get count of ready features
            var readyCount = spec.GetFeaturesCopy().Count(f => f.Status == FeatureStatus.Ready);

            if (readyCount == 0)
            {
                return $"⚠️ No Ready features in '{spec.Name}'. Use `process_features` with action 'list' to see draft features, " +
                       $"then use action 'promote' to mark features as Ready.";
            }

            // Trigger specification update to notify Wyvern
            _onSpecificationUpdated?.Invoke(spec.FilePath ?? "");

            SendMessage("success", $"Specification update triggered for {spec.Name}");

            return $"🔄 **Specification update triggered for '{spec.Name}'**\n\n" +
                   $"{readyCount} Ready feature(s) will be processed by Wyvern within the next 60 seconds.\n\n" +
                   $"You can check progress using the project list or agent status tools.";
        }

        /// <summary>
        /// Saves features to a JSON file in the project folder
        /// </summary>
        private async Task SaveFeaturesAsync(Specification spec)
        {
            if (string.IsNullOrEmpty(spec.Name))
                return;

            var folder = spec.ProjectFolder;
            if (string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(spec.FilePath))
            {
                folder = Path.GetDirectoryName(spec.FilePath);
            }

            if (string.IsNullOrEmpty(folder))
                return;

            try
            {
                var featuresPath = Path.Combine(folder, "specification.features.json");
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };

                var featuresData = new
                {
                    specificationVersion = spec.Version,
                    specificationContentHash = spec.ContentHash,
                    features = spec.Features
                };
                var json = System.Text.Json.JsonSerializer.Serialize(featuresData, options);
                await File.WriteAllTextAsync(featuresPath, json);
            }
            catch (Exception ex)
            {
                SendMessage("warning", $"Could not save features: {ex.Message}");
            }
        }
    }
}
