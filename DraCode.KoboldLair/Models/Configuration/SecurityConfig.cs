namespace DraCode.KoboldLair.Models.Configuration
{
    /// <summary>
    /// Security configuration for a project
    /// </summary>
    public class SecurityConfig
    {
        /// <summary>
        /// External paths (outside workspace) that are allowed for this project
        /// </summary>
        public List<string> AllowedExternalPaths { get; set; } = new();

        /// <summary>
        /// Sandbox mode for the project: "workspace" (default), "relaxed", or "strict"
        /// </summary>
        public string SandboxMode { get; set; } = "workspace";
    }
}
