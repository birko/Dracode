using System.Text;
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
        /// SHA-256 hash of the specification content for change detection
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// History of specification versions with metadata
        /// </summary>
        public List<SpecificationVersionHistoryEntry> VersionHistory { get; set; } = new();

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

        /// <summary>
        /// Increments version and updates content hash
        /// </summary>
        public void IncrementVersion()
        {
            Version++;
            UpdatedAt = DateTime.UtcNow;
            ContentHash = ComputeHash(Content);

            // Record history entry
            VersionHistory.Add(new SpecificationVersionHistoryEntry
            {
                Version = Version,
                Timestamp = UpdatedAt,
                ContentHash = ContentHash
            });
        }

        /// <summary>
        /// Computes SHA-256 hash of specification content
        /// </summary>
        public static string ComputeHash(string content)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Represents a single version in specification history
    /// </summary>
    public class SpecificationVersionHistoryEntry
    {
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public string? ChangeDescription { get; set; }
    }
}
