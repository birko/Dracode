namespace DraCode.Agent.Helpers
{
    public static class PathHelper
    {
        public static bool IsPathSafe(string path, string workingDirectory)
        {
            var fullPath = Path.GetFullPath(path);
            var workingPath = Path.GetFullPath(workingDirectory);
            return fullPath.StartsWith(workingPath);
        }
    }
}
