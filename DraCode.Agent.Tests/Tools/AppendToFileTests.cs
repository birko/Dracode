using DraCode.Agent.Tools;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DraCode.Agent.Tests.Tools;

public class AppendToFileTests : TestBase
{
    public AppendToFileTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Name_ShouldBe_append_to_file()
    {
        // Arrange
        var tool = new AppendToFile();

        // Assert
        tool.Name.Should().Be("append_to_file");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var tool = new AppendToFile();

        // Assert
        tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Execute_WithExistingFile_ShouldAppendContent()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "Original");
        var appendContent = " Appended";
        var input = CreateInput(("file_path", "test.txt"), ("content", appendContent));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be("Original Appended");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNonExistentFile_ShouldCreateFile()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        var content = "New content";
        var input = CreateInput(("file_path", "new.txt"), ("content", content));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "new.txt");

            // Assert
            result.Should().Be("OK");
            File.Exists(filePath).Should().BeTrue();
            File.ReadAllText(filePath).Should().Be(content);
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNestedPath_ShouldCreateDirectories()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        var content = "Nested";
        var input = CreateInput(
            ("file_path", "subdir/nested.txt"),
            ("content", content),
            ("create_directories", true)
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "subdir", "nested.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be(content);
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithCreateDirectoriesFalse_ShouldReturnError()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("file_path", "nonexistent/file.txt"),
            ("content", "content"),
            ("create_directories", false)
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: Directory does not exist:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithEmptyContent_ShouldKeepFileUnchanged()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "Original");
        var input = CreateInput(("file_path", "test.txt"), ("content", ""));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be("Original");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMultipleAppends_ShouldAccumulateContent()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "Start");
        var input = CreateInput(("file_path", "test.txt"), ("content", "1"));

        try
        {
            // Act
            tool.Execute(workspace, input);
            tool.Execute(workspace, CreateInput(("file_path", "test.txt"), ("content", "2")));
            tool.Execute(workspace, CreateInput(("file_path", "test.txt"), ("content", "3")));
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            File.ReadAllText(filePath).Should().Be("Start123");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMultilineContent_ShouldPreserveFormatting()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "Line 0");
        var input = CreateInput(("file_path", "test.txt"), ("content", "\nLine 1\nLine 2"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be("Line 0\nLine 1\nLine 2");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMissingFilePath_ShouldReturnError()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("content", "content"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error appending to file:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithEmptyFilePath_ShouldReturnError()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("file_path", ""), ("content", "content"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error appending to file:");
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
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("file_path", "../../etc/test.txt"),
            ("content", "content")
        );

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
        var tool = new AppendToFile
        {
            Options = new Agent.AgentOptions
            {
                AllowedExternalPaths = new List<string> { Path.GetTempPath() }
            }
        };
        var workspace = GetTestWorkspace();
        var tempFile = Path.Combine(Path.GetTempPath(), $"dracode-test-{Guid.NewGuid()}.txt");
        File.WriteAllText(tempFile, "Original ");
        var input = CreateInput(("file_path", tempFile), ("content", "Appended"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(tempFile).Should().Be("Original Appended");
        }
        finally
        {
            CleanupWorkspace(workspace);
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Execute_WithNullContent_ShouldAppendEmptyString()
    {
        // Arrange
        var tool = new AppendToFile();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "Original");
        var input = CreateInput(("file_path", "test.txt"), ("content", null!));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be("Original");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }
}
