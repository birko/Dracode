using DraCode.Agent.Tools;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DraCode.Agent.Tests.Tools;

public class WriteFileTests : TestBase
{
    public WriteFileTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Name_ShouldBe_write_file()
    {
        // Arrange
        var tool = new WriteFile();

        // Assert
        tool.Name.Should().Be("write_file");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var tool = new WriteFile();

        // Assert
        tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Execute_WithNewFile_ShouldCreateFile()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var content = "Hello, World!";
        var input = CreateInput(("file_path", "test.txt"), ("content", content));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

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
    public void Execute_WithNestedFile_ShouldCreateDirectories()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var content = "Nested content";
        var input = CreateInput(
            ("file_path", "subdir/nested/test.txt"),
            ("content", content),
            ("create_directories", true)
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "subdir", "nested", "test.txt");

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
    public void Execute_WithCreateDirectoriesFalse_ShouldReturnError()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("file_path", "nonexistent/test.txt"),
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
    public void Execute_WithExistingFileAndCheckExistsTrue_ShouldReturnWarning()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "existing.txt", "original content");
        var input = CreateInput(
            ("file_path", "existing.txt"),
            ("content", "new content"),
            ("check_exists", true)
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Warning: File 'existing.txt' already exists.");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithExistingFileAndCheckExistsFalse_ShouldOverwrite()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "existing.txt", "original content");
        var newContent = "new content";
        var input = CreateInput(
            ("file_path", "existing.txt"),
            ("content", newContent),
            ("check_exists", false)
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "existing.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be(newContent);
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithEmptyContent_ShouldCreateEmptyFile()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("file_path", "empty.txt"), ("content", ""));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "empty.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().BeEmpty();
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMultilineContent_ShouldPreserveNewlines()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var content = "Line 1\nLine 2\nLine 3";
        var input = CreateInput(("file_path", "multiline.txt"), ("content", content));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "multiline.txt");

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
    public void Execute_WithSpecialCharacters_ShouldWriteCorrectly()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var content = "Special: <>&\"'\\t\\n";
        var input = CreateInput(("file_path", "special.txt"), ("content", content));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "special.txt");

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
    public void Execute_WithMissingFilePath_ShouldReturnError()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("content", "content"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error writing file:");
            result.Should().Contain("file_path");
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
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("file_path", ""), ("content", "content"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Error writing file: file_path is required");
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
        var tool = new WriteFile();
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
        var tool = new WriteFile
        {
            Options = new Agent.AgentOptions
            {
                AllowedExternalPaths = new List<string> { Path.GetTempPath() }
            }
        };
        var workspace = GetTestWorkspace();
        var tempFile = Path.Combine(Path.GetTempPath(), $"dracode-test-{Guid.NewGuid()}.txt");
        var input = CreateInput(("file_path", tempFile), ("content", "external content"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("OK");
            File.Exists(tempFile).Should().BeTrue();
        }
        finally
        {
            CleanupWorkspace(workspace);
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Execute_WithMissingContent_ShouldCreateEmptyFile()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("file_path", "no-content.txt"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "no-content.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().BeEmpty();
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNullContent_ShouldCreateEmptyFile()
    {
        // Arrange
        var tool = new WriteFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("file_path", "null-content.txt"), ("content", null!));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "null-content.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().BeEmpty();
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }
}
