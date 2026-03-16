using System.Text.Json;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for deleting features from specifications. Only Draft features can be deleted.
    /// </summary>
    public class DeleteFeatureTool : Tool
    {
        private readonly Dictionary<string, Specification> _specifications;

        public DeleteFeatureTool(Dictionary<string, Specification> specifications)
        {
            _specifications = specifications;
        }

        public override string Name => "delete_feature";

        public override string Description =>
            "Delete a feature from a specification. Only features with 'Draft' status can be deleted. " +
            "Features that are Ready, InProgress, or Completed cannot be removed.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                specification_name = new
                {
                    type = "string",
                    description = "Name of the specification"
                },
                feature_name = new
                {
                    type = "string",
                    description = "Name of the feature to delete"
                },
                confirm = new
                {
                    type = "string",
                    description = "Type 'yes' to confirm deletion",
                    @enum = new[] { "yes" }
                }
            },
            required = new[] { "specification_name", "feature_name", "confirm" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("specification_name", out var specNameObj))
                return "Error: specification_name is required";

            if (!input.TryGetValue("feature_name", out var featureNameObj))
                return "Error: feature_name is required";

            if (!input.TryGetValue("confirm", out var confirmObj) || confirmObj?.ToString()?.ToLower() != "yes")
                return "Error: confirmation required. Set confirm to 'yes' to delete.";

            var specName = specNameObj.ToString() ?? "";
            var featureName = featureNameObj.ToString() ?? "";

            if (!_specifications.TryGetValue(specName, out var spec))
                return $"Error: Specification '{specName}' not found.";

            string? result = null;
            spec.WithFeatures(features =>
            {
                var feature = features.FirstOrDefault(f => f.Name.Equals(featureName, StringComparison.OrdinalIgnoreCase));
                if (feature == null)
                {
                    result = $"Error: Feature '{featureName}' not found in specification '{specName}'.";
                    return;
                }

                // Only allow deleting Draft features
                if (feature.Status != FeatureStatus.Draft)
                {
                    result = $"Cannot delete feature '{featureName}': Status is '{feature.Status}'. " +
                             "Only features with 'Draft' status can be deleted. " +
                             "Features that are already being processed cannot be removed.";
                    return;
                }

                features.Remove(feature);
                spec.UpdatedAt = DateTime.UtcNow;
                result = null; // success
            });

            if (result != null)
                return result;

            // Save updated features
            await SaveFeaturesAsync(spec);

            SendMessage("success", $"Feature deleted: {featureName}");
            return $"✅ Feature '{featureName}' has been deleted from specification '{specName}'.";
        }

        private async Task SaveFeaturesAsync(Specification spec)
        {
            var folder = spec.ProjectFolder;
            if (string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(spec.FilePath))
                folder = Path.GetDirectoryName(spec.FilePath);

            if (string.IsNullOrEmpty(folder)) return;

            try
            {
                var featuresPath = Path.Combine(folder, "specification.features.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
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
    }
}
