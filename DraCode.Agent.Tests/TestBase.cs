using Xunit.Abstractions;

namespace DraCode.Agent.Tests;

public abstract class TestBase
{
    protected TestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    protected ITestOutputHelper Output { get; }

    protected string GetTestWorkspace()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dracode-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(workspace);
        Output.WriteLine($"Created test workspace: {workspace}");
        return workspace;
    }

    protected void CleanupWorkspace(string workspace)
    {
        try
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, true);
                Output.WriteLine($"Cleaned up test workspace: {workspace}");
            }
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Failed to cleanup workspace: {ex.Message}");
        }
    }

    protected string CreateTestFile(string workspace, string relativePath, string content)
    {
        var fullPath = Path.Combine(workspace, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
        Output.WriteLine($"Created test file: {relativePath}");
        return fullPath;
    }

    protected Dictionary<string, object> CreateInput(params (string key, object value)[] values)
    {
        var dict = new Dictionary<string, object>();
        foreach (var (key, value) in values)
        {
            dict[key] = value;
        }
        return dict;
    }
}
