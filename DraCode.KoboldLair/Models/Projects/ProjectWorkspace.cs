namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Where a project's code is (TASK-179): an imported project's <c>SourcePath</c> when it exists, else <c>workspace/</c>
    /// in the project folder. The rule Drake applies when it gives a Kobold its working directory.
    /// </summary>
    public static class ProjectWorkspace
    {
        public static string For(Project project)
        {
            if (project.Metadata.TryGetValue("IsExistingProject", out var isExisting) && isExisting == "true" &&
                project.Metadata.TryGetValue("SourcePath", out var sourcePath) &&
                !string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath))
            {
                return sourcePath;
            }

            return Path.Combine(project.Paths.Output, "workspace");
        }
    }
}
