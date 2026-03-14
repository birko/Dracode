namespace DraCode.Agent.Helpers
{
    public static class PathHelper
    {
        /// <summary>
        /// Gets the canonical path with correct capitalization from the filesystem.
        /// On Windows, returns the actual path with proper case. On other platforms, returns the full path.
        /// </summary>
        private static string GetCanonicalPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var fullPath = Path.GetFullPath(path);

            // On Windows, get the actual case from the filesystem
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    if (Directory.Exists(fullPath))
                    {
                        return new DirectoryInfo(fullPath).FullName;
                    }
                    else if (File.Exists(fullPath))
                    {
                        return new FileInfo(fullPath).FullName;
                    }
                }
                catch
                {
                    // If we can't get the actual case, fall through to return the full path
                }
            }

            return fullPath;
        }

        public static bool IsPathSafe(string path, string workingDirectory)
        {
            var fullPath = Path.GetFullPath(path);
            var workingPath = Path.GetFullPath(workingDirectory);
            return IsUnderDirectory(fullPath, workingPath);
        }

        /// <summary>
        /// Checks if a path is safe to access, considering both the workspace and allowed external paths.
        /// Uses case-insensitive comparison on Windows to handle case variations.
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
        /// Uses case-insensitive comparison for case-insensitive filesystems (Windows).
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
            // Check both primary and alternate separators (handles mixed / and \ on Windows)
            string dirWithPrimarySep = normalizedDir + Path.DirectorySeparatorChar;
            string dirWithAltSep = normalizedDir + Path.AltDirectorySeparatorChar;
            return normalizedPath.StartsWith(dirWithPrimarySep, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(dirWithAltSep, StringComparison.OrdinalIgnoreCase);
        }
    }
}
