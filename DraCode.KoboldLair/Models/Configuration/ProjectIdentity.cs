namespace DraCode.KoboldLair.Models.Configuration
{
    /// <summary>
    /// Identity information for a project
    /// </summary>
    public class ProjectIdentity
    {
        /// <summary>
        /// Unique identifier for the project
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the project
        /// </summary>
        public string? Name { get; set; }
    }
}
