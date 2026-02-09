using DraCode.KoboldLair.Models.Agents;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Filters file lists based on implementation plan requirements.
    /// Reduces context size by only including files relevant to the plan.
    /// </summary>
    public class PlanFileFilterService
    {
        private readonly ILogger<PlanFileFilterService>? _logger;

        // Common config and project files that should always be included
        private static readonly HashSet<string> ImportantFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "package.json", "package-lock.json", "tsconfig.json", "jsconfig.json",
            "appsettings.json", "appsettings.Development.json", "appsettings.Production.json",
            "web.config", "app.config", ".env", ".env.local",
            "requirements.txt", "setup.py", "pyproject.toml",
            "Gemfile", "Gemfile.lock", "composer.json",
            "pom.xml", "build.gradle", "settings.gradle",
            "Cargo.toml", "go.mod", "go.sum",
            "README.md", "LICENSE", ".gitignore"
        };

        private static readonly HashSet<string> ProjectFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".csproj", ".vbproj", ".fsproj", ".sln", ".slnx"
        };

        public PlanFileFilterService(ILogger<PlanFileFilterService>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Filters the file list to only include files relevant to the implementation plan.
        /// </summary>
        /// <param name="allFiles">Complete list of files in the workspace</param>
        /// <param name="plan">Implementation plan containing file operations</param>
        /// <returns>Filtered list of relevant files</returns>
        public List<string> FilterRelevantFiles(List<string> allFiles, KoboldImplementationPlan plan)
        {
            if (allFiles == null || allFiles.Count == 0)
            {
                return new List<string>();
            }

            if (plan == null || plan.Steps.Count == 0)
            {
                // No plan - return original list
                return allFiles;
            }

            var relevantFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Add files mentioned in the plan
            foreach (var step in plan.Steps)
            {
                foreach (var file in step.FilesToCreate.Concat(step.FilesToModify))
                {
                    relevantFiles.Add(NormalizePath(file));
                }
            }

            // 2. Add related files from the same directories
            var relevantDirs = GetDirectories(relevantFiles);
            foreach (var file in allFiles)
            {
                var normalizedFile = NormalizePath(file);
                var fileDir = GetDirectory(normalizedFile);

                // Include if in same directory as plan-mentioned files
                if (!string.IsNullOrEmpty(fileDir) && relevantDirs.Contains(fileDir))
                {
                    relevantFiles.Add(normalizedFile);
                }
            }

            // 3. Add important config and project files
            foreach (var file in allFiles)
            {
                var normalizedFile = NormalizePath(file);
                var fileName = Path.GetFileName(normalizedFile);
                var extension = Path.GetExtension(normalizedFile);

                if (ImportantFiles.Contains(fileName) || 
                    ProjectFileExtensions.Contains(extension))
                {
                    relevantFiles.Add(normalizedFile);
                }
            }

            // 4. Add parent directory files (for imports/dependencies)
            var parentDirs = GetParentDirectories(relevantDirs);
            foreach (var file in allFiles)
            {
                var normalizedFile = NormalizePath(file);
                var fileDir = GetDirectory(normalizedFile);

                if (!string.IsNullOrEmpty(fileDir) && parentDirs.Contains(fileDir))
                {
                    // Only add code files from parent dirs, not all files
                    if (IsCodeFile(normalizedFile))
                    {
                        relevantFiles.Add(normalizedFile);
                    }
                }
            }

            // Convert back to list and sort
            var result = relevantFiles
                .Where(f => allFiles.Contains(f, StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger?.LogDebug(
                "Filtered files for plan {PlanId}: {OriginalCount} → {FilteredCount} files",
                plan.TaskId, allFiles.Count, result.Count);

            return result;
        }

        /// <summary>
        /// Normalizes path separators to forward slashes for consistent comparison
        /// </summary>
        private string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        /// <summary>
        /// Gets unique directories from a list of file paths
        /// </summary>
        private HashSet<string> GetDirectories(IEnumerable<string> files)
        {
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var dir = GetDirectory(file);
                if (!string.IsNullOrEmpty(dir))
                {
                    dirs.Add(dir);
                }
            }
            return dirs;
        }

        /// <summary>
        /// Gets the directory portion of a file path
        /// </summary>
        private string GetDirectory(string filePath)
        {
            var lastSlash = filePath.LastIndexOf('/');
            if (lastSlash > 0)
            {
                return filePath.Substring(0, lastSlash);
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets parent directories (one level up) from a set of directories
        /// </summary>
        private HashSet<string> GetParentDirectories(HashSet<string> directories)
        {
            var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in directories)
            {
                var parent = GetDirectory(dir);
                if (!string.IsNullOrEmpty(parent) && parent != dir)
                {
                    parents.Add(parent);
                }
            }
            return parents;
        }

        /// <summary>
        /// Checks if a file is a code file based on extension
        /// </summary>
        private bool IsCodeFile(string filePath)
        {
            var codeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".vb", ".fs", ".ts", ".tsx", ".js", ".jsx", ".py", ".rb",
                ".java", ".kt", ".go", ".rs", ".cpp", ".c", ".h", ".hpp",
                ".php", ".swift", ".m", ".mm", ".scala", ".clj"
            };

            var ext = Path.GetExtension(filePath);
            return codeExtensions.Contains(ext);
        }
    }
}
