using System.Text.Json;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Canonical persistence for project specifications and their features (TASK-043).
    /// <para>
    /// Owns the on-disk layout — <c>{projectFolder}/specification.md</c> for the markdown body and
    /// <c>{projectFolder}/specification.features.json</c> for the features (wrapped format carrying
    /// <c>specificationVersion</c> + <c>specificationContentHash</c>). Before this service the same
    /// read/write logic was copy-pasted across the Dragon council tools
    /// (<c>SpecificationManagementTool</c>, <c>FeatureManagementTool</c>, <c>DeleteFeatureTool</c>,
    /// <c>ProcessFeaturesTool</c>) — three identical private <c>SaveFeaturesAsync</c> bodies and a
    /// static <c>LoadFeaturesAsync</c> — which drifted from the (future) REST surface. Both the tools
    /// and the <c>/api/v1</c> resource endpoints now route through here, so they cannot diverge.
    /// </para>
    /// <para>
    /// The <b>persistence primitives are static</b> (they operate purely on a folder + a
    /// <see cref="Specification"/>, no instance state). The <b>instance</b> surface adds
    /// projects-path-relative, name-based load/create/update used by the stateless REST handlers
    /// (which, unlike the Dragon session, hold no in-memory specification cache).
    /// </para>
    /// </summary>
    public class SpecificationService
    {
        /// <summary>File name of the markdown specification body within a project folder.</summary>
        public const string SpecFileName = "specification.md";

        /// <summary>File name of the features sidecar (wrapped JSON) within a project folder.</summary>
        public const string FeaturesFileName = "specification.features.json";

        private readonly string _projectsPath;

        public SpecificationService(string? projectsPath = "./projects")
        {
            _projectsPath = projectsPath ?? "./projects";
        }

        // ---- Path helpers ---------------------------------------------------

        /// <summary>
        /// Sanitizes a project/specification name into a folder name (lowercase, spaces → dashes,
        /// invalid path chars stripped). Mirrors the prior tool logic so existing folders resolve.
        /// </summary>
        public static string SanitizeProjectName(string projectName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", projectName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim().Replace(" ", "-").ToLowerInvariant();
        }

        /// <summary>Resolves the consolidated project folder for a spec name: <c>{projectsPath}/{sanitized}</c>.</summary>
        public string ResolveProjectFolder(string name) =>
            Path.Combine(_projectsPath, SanitizeProjectName(name));

        // ---- Static persistence primitives (shared with the Dragon tools) ---

        /// <summary>
        /// Writes the features sidecar for <paramref name="spec"/> in wrapped format
        /// (<c>specificationVersion</c> + <c>specificationContentHash</c> + <c>features</c>). Resolves the
        /// folder from <see cref="Specification.ProjectFolder"/>, falling back to the spec file's directory.
        /// No-ops when no folder can be determined. Throws on I/O failure (callers decide how to surface it).
        /// </summary>
        public static async Task PersistFeaturesAsync(Specification spec)
        {
            var folder = ResolveFolder(spec);
            if (string.IsNullOrEmpty(folder))
                return;

            var featuresPath = Path.Combine(folder, FeaturesFileName);
            var featuresData = new Dictionary<string, object?>
            {
                ["specificationVersion"] = spec.Version,
                ["specificationContentHash"] = spec.ContentHash,
                ["features"] = spec.Features
            };
            if (spec.LifecycleLayer is { } lifecycle)
                featuresData["lifecycleLayer"] = lifecycle; // TASK-211
            var json = JsonSerializer.Serialize(featuresData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(featuresPath, json);
        }

        /// <summary>
        /// Loads features from the sidecar into <paramref name="spec"/>. Handles both the wrapped format
        /// (object with <c>features</c> + version/hash) and the legacy bare-array format. Silently no-ops
        /// when the file is absent or unreadable (features stay empty) — preserving the prior tool behavior.
        /// </summary>
        public static async Task LoadFeaturesAsync(Specification spec, string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            try
            {
                var featuresPath = Path.Combine(folderPath, FeaturesFileName);
                if (!File.Exists(featuresPath))
                    return;

                var json = await File.ReadAllTextAsync(featuresPath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("features", out var featuresProp))
                {
                    var features = JsonSerializer.Deserialize<List<Feature>>(featuresProp.GetRawText());
                    if (features != null)
                        spec.Features = features;

                    if (doc.RootElement.TryGetProperty("specificationVersion", out var versionProp))
                        spec.Version = versionProp.GetInt32();
                    if (doc.RootElement.TryGetProperty("specificationContentHash", out var hashProp))
                        spec.ContentHash = hashProp.GetString() ?? string.Empty;
                    if (doc.RootElement.TryGetProperty("lifecycleLayer", out var lifecycleProp) &&
                        lifecycleProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        spec.LifecycleLayer = lifecycleProp.GetBoolean();
                }
                else
                {
                    // Legacy bare-array format
                    var features = JsonSerializer.Deserialize<List<Feature>>(json);
                    if (features != null)
                        spec.Features = features;
                }
            }
            catch
            {
                // Silently ignore load errors — features remain empty (matches prior tool behavior).
            }
        }

        /// <summary>
        /// The person's answer, given to Dragon when the specification in <paramref name="folderPath"/> was created, to
        /// whether its project uses the lifecycle layer (TASK-211); null when it was not asked or cannot be read.
        /// </summary>
        public static async Task<bool?> ReadLifecycleChoiceAsync(string folderPath)
        {
            var spec = new Specification();
            await LoadFeaturesAsync(spec, folderPath);
            return spec.LifecycleLayer;
        }

        private static string? ResolveFolder(Specification spec)
        {
            var folder = spec.ProjectFolder;
            if (string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(spec.FilePath))
                folder = Path.GetDirectoryName(spec.FilePath);
            return folder;
        }

        // ---- Instance, name-based surface (for the stateless REST handlers) -

        /// <summary>
        /// Loads a full specification (markdown body + features) by name from the consolidated layout,
        /// or <c>null</c> when no <c>specification.md</c> exists for that name.
        /// </summary>
        public async Task<Specification?> LoadByNameAsync(string name)
        {
            var folder = ResolveProjectFolder(name);
            var specPath = Path.Combine(folder, SpecFileName);
            if (!File.Exists(specPath))
                return null;

            var content = await File.ReadAllTextAsync(specPath);
            var spec = new Specification
            {
                Name = name,
                FilePath = specPath,
                ProjectFolder = folder,
                Content = content
            };
            await LoadFeaturesAsync(spec, folder);
            return spec;
        }

        /// <summary>
        /// Writes the markdown body for a spec (creating the project folder if needed) and returns the
        /// resulting <see cref="Specification"/>. Used by <c>PUT /projects/{id}/specification</c>; when the
        /// spec already exists its version is bumped and the features sidecar re-persisted with the new hash.
        /// </summary>
        public async Task<Specification> SaveContentAsync(string name, string content)
        {
            var folder = ResolveProjectFolder(name);
            Directory.CreateDirectory(folder);
            var specPath = Path.Combine(folder, SpecFileName);

            var existing = await LoadByNameAsync(name);
            await File.WriteAllTextAsync(specPath, content);

            var spec = existing ?? new Specification
            {
                Name = name,
                FilePath = specPath,
                ProjectFolder = folder
            };
            spec.Content = content;
            if (existing != null)
            {
                // Updating: bump version + hash, then re-persist the sidecar so version/hash stay in sync.
                spec.IncrementVersion();
                await PersistFeaturesAsync(spec);
            }
            else
            {
                spec.ContentHash = Specification.ComputeHash(content);
            }
            return spec;
        }

        /// <summary>The heading definitions are kept under in the specification (TASK-207).</summary>
        public const string DefinitionsHeading = "## Definitions";

        /// <summary>
        /// Writes what the person defined into the specification (TASK-207, FEATURE-078 D20): one line, "- question — answer",
        /// at the end of the "## Definitions" section, which is added at the end when the specification has none. Saved
        /// through <see cref="SaveContentAsync"/>, so the version is bumped as for any change. Nothing happens for a project
        /// with no specification.
        /// </summary>
        public async Task AppendDefinitionAsync(string name, string question, string answer)
        {
            var spec = await LoadByNameAsync(name);
            if (spec == null)
                return;

            var line = $"- {OneLine(question)} — {OneLine(answer)}";
            var lines = spec.Content.Replace("\r\n", "\n").Split('\n').ToList();
            var heading = lines.FindIndex(l => l.Trim() == DefinitionsHeading);
            if (heading < 0)
            {
                while (lines.Count > 0 && lines[^1].Trim().Length == 0)
                    lines.RemoveAt(lines.Count - 1);
                lines.AddRange(new[] { "", DefinitionsHeading, "", line, "" });
            }
            else
            {
                // The section ends at the next heading of the same or a higher level, or at the end
                var end = lines.FindIndex(heading + 1, l => l.StartsWith("# ") || l.StartsWith("## "));
                var insertAt = end < 0 ? lines.Count : end;
                while (insertAt > heading + 1 && lines[insertAt - 1].Trim().Length == 0)
                    insertAt--;
                lines.Insert(insertAt, line);
                if (end >= 0 && lines[insertAt + 1].Trim().Length > 0)
                    lines.Insert(insertAt + 1, "");
            }
            await SaveContentAsync(name, string.Join("\n", lines));
        }

        private static string OneLine(string text) => System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
    }
}
