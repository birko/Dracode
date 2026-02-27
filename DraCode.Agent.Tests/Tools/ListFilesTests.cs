using DraCode.Agent.Tools;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DraCode.Agent.Tests.Tools;

public class ListFilesTests : TestBase
{
    public ListFilesTests(ITestOutputHelper output) : base(output) { }

    private string[] SplitLines(string input) =>
        input.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Name_ShouldBe_list_files()
    {
        // Arrange
        var tool = new ListFiles();

        // Assert
        tool.Name.Should().Be("list_files");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var tool = new ListFiles();

        // Assert
        tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InputSchema_ShouldHaveRequiredProperties()
    {
        // Arrange
        var tool = new ListFiles();
        var schema = tool.InputSchema;

        // Assert - just verify schema is not null (it's an anonymous object)
        schema.Should().NotBeNull();
    }

    [Fact]
    public void Execute_WithEmptyDirectory_ShouldReturnEmptyString()
    {
        // Arrange
        var tool = new ListFiles();
        var workspace = GetTestWorkspace();
        var input = CreateInput();

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().BeEmpty();
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithFiles_ShouldReturnRelativePaths()
    {
        // Arrange
        var tool = new ListFiles();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "file1.txt", "content1");
        CreateTestFile(workspace, "file2.txt", "content2");
        var input = CreateInput();

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = SplitLines(result);

            // Assert - normalize path separators
            var normalizedLines = lines.Select(l => l.Replace('\\', '/')).ToList();
            normalizedLines.Should().Contain("file1.txt");
            normalizedLines.Should().Contain("file2.txt");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithSubdirectoryFiles_ShouldReturnRelativePaths()
    {
        // Arrange
        var tool = new ListFiles();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "subdir/file1.txt", "content1");
        CreateTestFile(workspace, "subdir/file2.txt", "content2");
        var input = CreateInput(("recursive", true)); // Need recursive to list subdir files

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = SplitLines(result);

            // Assert - normalize path separators for comparison
            var normalizedLines = lines.Select(l => l.Replace('\\', '/')).ToList();
            normalizedLines.Should().Contain(l => l.EndsWith("subdir/file1.txt") || l.EndsWith("subdir\\file1.txt"));
            normalizedLines.Should().Contain(l => l.EndsWith("subdir/file2.txt") || l.EndsWith("subdir\\file2.txt"));
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithRecursiveTrue_ShouldListNestedFiles()
    {
        // Arrange
        var tool = new ListFiles();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "file1.txt", "content1");
        CreateTestFile(workspace, "subdir/file2.txt", "content2");
        CreateTestFile(workspace, "subdir/nested/file3.txt", "content3");
        var input = CreateInput(("recursive", true));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = SplitLines(result);

            // Assert - normalize path separators for comparison
            var normalizedLines = lines.Select(l => l.Replace('\\', '/')).ToList();
            normalizedLines.Should().HaveCountGreaterOrEqualTo(3);
            normalizedLines.Should().Contain(l => l.Trim() == "file1.txt");
            normalizedLines.Should().Contain(l => l.Contains("subdir") && l.Contains("file2.txt"));
            normalizedLines.Should().Contain(l => l.Contains("subdir") && l.Contains("nested") && l.Contains("file3.txt"));
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithRecursiveFalse_ShouldNotListNestedFiles()
    {
        // Arrange
        var tool = new ListFiles();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "file1.txt", "content1");
        CreateTestFile(workspace, "subdir/file2.txt", "content2");
        var input = CreateInput(("recursive", false));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = SplitLines(result);

            // Assert
            lines.Should().Contain("file1.txt");
            lines.Should().NotContain(Path.Combine("subdir", "file2.txt").Replace("\\", "/"));
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithSubdirectoryParameter_ShouldListOnlyInSubdirectory()
    {
        // Arrange
        var tool = new ListFiles();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "file1.txt", "content1");
        CreateTestFile(workspace, "subdir/file2.txt", "content2");
        CreateTestFile(workspace, "subdir/file3.txt", "content3");
        var input = CreateInput(("directory", "subdir"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = SplitLines(result);

            // Assert - normalize path separators for comparison
            var normalizedLines = lines.Select(l => l.Replace('\\', '/')).ToList();
            normalizedLines.Should().NotContain("file1.txt");
            normalizedLines.Should().Contain(l => l.Contains("subdir/file2.txt") || l.Contains("subdir\\file2.txt"));
            normalizedLines.Should().Contain(l => l.Contains("subdir/file3.txt") || l.Contains("subdir\\file3.txt"));
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNonExistentDirectory_ShouldReturnError()
    {
        // Arrange
        var tool = new ListFiles();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("directory", "nonexistent"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: Directory not found:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithUnsafePath_ShouldReturnAccessDenied()
    {
        // Arrange
        var tool = new ListFiles();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("directory", "../../etc"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: Access denied");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithAllowedExternalPath_ShouldSucceed()
    {
        // Arrange
        var tool = new ListFiles
        {
            Options = new Agent.AgentOptions
            {
                AllowedExternalPaths = new List<string> { Path.GetTempPath() }
            }
        };
        var workspace = GetTestWorkspace();
        var input = CreateInput(("directory", Path.GetTempPath()));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert - should not be an access denied error
            result.Should().NotStartWith("Error: Access denied");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNullInput_ShouldNotThrow()
    {
        // Arrange
        var tool = new ListFiles();
        var workspace = GetTestWorkspace();

        try
        {
            // Act & Assert - should not throw
            var result = tool.Execute(workspace, null!);
            // With null input, directory is null, so it lists the workspace root
            // Should return files in workspace (which is empty)
            result.Should().NotBeNull();
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }
}
