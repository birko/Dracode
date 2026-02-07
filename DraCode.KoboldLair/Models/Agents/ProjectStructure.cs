namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Captures the file structure and organization patterns of a project at analysis time.
    /// Helps Kobolds understand where files should be created or modified.
    /// </summary>
    public class ProjectStructure
    {
        /// <summary>
        /// List of existing files at time of analysis (relative paths from workspace root)
        /// </summary>
        public List<string> ExistingFiles { get; set; } = new();

        /// <summary>
        /// Discovered file naming conventions (e.g., "csharp-classes" -> "PascalCase")
        /// </summary>
        public Dictionary<string, string> NamingConventions { get; set; } = new();

        /// <summary>
        /// Directory purposes and organization (e.g., "src/models" -> "Domain models and entities")
        /// </summary>
        public Dictionary<string, string> DirectoryPurposes { get; set; } = new();

        /// <summary>
        /// Recommended locations for new file types (e.g., "controller" -> "src/controllers/")
        /// </summary>
        public Dictionary<string, string> FileLocationGuidelines { get; set; } = new();

        /// <summary>
        /// General architecture notes discovered during analysis
        /// </summary>
        public string? ArchitectureNotes { get; set; }
    }
}
