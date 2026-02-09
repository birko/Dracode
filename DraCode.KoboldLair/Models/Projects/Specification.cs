using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Represents a project specification with associated features
    /// </summary>
    public class Specification
    {
        private readonly object _lock = new object();
        
        /// <summary>
        /// Unique identifier for the specification
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Name of the specification
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Full markdown content of the specification
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Path to the specification file on disk
        /// </summary>
        public string FilePath { get; set; } = "";

        /// <summary>
        /// Path to the project folder containing this specification and related files
        /// </summary>
        public string ProjectFolder { get; set; } = "";

        /// <summary>
        /// ID of the project this specification belongs to
        /// </summary>
        public string ProjectId { get; set; } = "";

        /// <summary>
        /// Features defined in this specification
        /// </summary>
        public List<Feature> Features { get; set; } = new();

        /// <summary>
        /// When the specification was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the specification was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Version number of the specification
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
        
        /// <summary>
        /// Gets a thread-safe snapshot of features
        /// </summary>
        public List<Feature> GetFeaturesCopy()
        {
            lock (_lock)
            {
                return new List<Feature>(Features);
            }
        }
        
        /// <summary>
        /// Executes an action on features with thread-safety
        /// </summary>
        public void WithFeatures(Action<List<Feature>> action)
        {
            lock (_lock)
            {
                action(Features);
            }
        }
        
        /// <summary>
        /// Executes a function on features with thread-safety and returns result
        /// </summary>
        public T WithFeatures<T>(Func<List<Feature>, T> func)
        {
            lock (_lock)
            {
                return func(Features);
            }
        }
    }
}
