namespace DraCode.Agent.Helpers
{
    public static class PathHelper
    {
        public static bool IsPathSafe(string path, string workingDirectory)
        {
            var fullPath = Path.GetFullPath(path);
            var workingPath = Path.GetFullPath(workingDirectory);
            return IsUnderDirectory(fullPath, workingPath);
        }

        /// <summary>
        /// Checks if a path is safe to access, considering both the workspace and allowed external paths.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <param name="workingDirectory">The workspace directory</param>
        /// <param name="allowedExternalPaths">Optional list of allowed external paths</param>
        /// <returns>True if the path is within workspace or an allowed external path</returns>
        public static bool IsPathSafe(string path, string workingDirectory, IEnumerable<string>? allowedExternalPaths)
        {
            var fullPath = Path.GetFullPath(path);
            var workingPath = Path.GetFullPath(workingDirectory);

            // Check workspace first
            if (IsUnderDirectory(fullPath, workingPath))
                return true;

            // Check allowed external paths
            if (allowedExternalPaths != null)
            {
                foreach (var allowed in allowedExternalPaths)
                {
                    if (string.IsNullOrEmpty(allowed))
                        continue;

                    var allowedFull = Path.GetFullPath(allowed);
                    if (IsUnderDirectory(fullPath, allowedFull))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a path is exactly at or under a directory, using proper boundary matching.
        /// Prevents false matches where e.g. "C:\Source\Birko.Data" would match "C:\Source\Birko".
        /// </summary>
        /// <param name="fullPath">The full path to check</param>
        /// <param name="directoryPath">The directory that should contain the path</param>
        /// <returns>True if fullPath is the directory itself or a child of it</returns>
        public static bool IsUnderDirectory(string fullPath, string directoryPath)
        {
            var normalizedDir = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Exact match (the path IS the directory)
            if (string.Equals(normalizedPath, normalizedDir, StringComparison.OrdinalIgnoreCase))
                return true;

            // Child path must start with directory + separator
            return fullPath.StartsWith(normalizedDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
