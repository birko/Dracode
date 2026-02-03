namespace DraCode.Agent.Helpers
{
    public static class PathHelper
    {
        public static bool IsPathSafe(string path, string workingDirectory)
        {
            var fullPath = Path.GetFullPath(path);
            var workingPath = Path.GetFullPath(workingDirectory);
            return fullPath.StartsWith(workingPath, StringComparison.OrdinalIgnoreCase);
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
            if (fullPath.StartsWith(workingPath, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check allowed external paths
            if (allowedExternalPaths != null)
            {
                foreach (var allowed in allowedExternalPaths)
                {
                    if (string.IsNullOrEmpty(allowed))
                        continue;

                    var allowedFull = Path.GetFullPath(allowed);
                    if (fullPath.StartsWith(allowedFull, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
