using DraCode.Agent.Tools;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DraCode.Agent.Tests.Tools;

public class EditFileTests : TestBase
{
    public EditFileTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Name_ShouldBe_edit_file()
    {
        // Arrange
        var tool = new EditFile();

        // Assert
        tool.Name.Should().Be("edit_file");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var tool = new EditFile();

        // Assert
        tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Execute_WithExistingText_ShouldReplace()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var content = "Hello World";
        CreateTestFile(workspace, "test.txt", content);
        var input = CreateInput(
            ("file_path", "test.txt"),
            ("old_text", "World"),
            ("new_text", "Universe")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be("Hello Universe");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithExactMatch_ShouldReplace()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var content = "Line 1\nLine 2\nLine 3";
        CreateTestFile(workspace, "test.txt", content);
        var input = CreateInput(
            ("file_path", "test.txt"),
            ("old_text", "Line 2"),
            ("new_text", "Modified Line 2")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be("Line 1\nModified Line 2\nLine 3");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithWhitespaceSensitive_ShouldMatchExact()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var content = "  Indented  ";
        CreateTestFile(workspace, "test.txt", content);
        var input = CreateInput(
            ("file_path", "test.txt"),
            ("old_text", "  Indented  "),
            ("new_text", "Modified")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be("Modified");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNonExistentFile_ShouldReturnError()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("file_path", "nonexistent.txt"),
            ("old_text", "old"),
            ("new_text", "new")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: File does not exist:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithOldTextNotFound_ShouldReturnErrorWithPreview()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var content = "Actual file content here";
        CreateTestFile(workspace, "test.txt", content);
        var input = CreateInput(
            ("file_path", "test.txt"),
            ("old_text", "nonexistent text"),
            ("new_text", "new")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: old_text not found in file.");
            result.Should().Contain("Actual file content here");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMultipleOccurrences_ShouldReturnError()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var content = "repeat repeat repeat";
        CreateTestFile(workspace, "test.txt", content);
        var input = CreateInput(
            ("file_path", "test.txt"),
            ("old_text", "repeat"),
            ("new_text", "changed")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: old_text appears 3 times");
            result.Should().Contain("more specific text block");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithLargeFile_ShouldPreviewWhenNotFound()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var largeContent = new string('A', 10_000);
        CreateTestFile(workspace, "large.txt", largeContent);
        var input = CreateInput(
            ("file_path", "large.txt"),
            ("old_text", "nonexistent"),
            ("new_text", "new")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Contain("truncated");
            result.Should().Contain("10000 chars total");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithEmptyOldText_ShouldReturnError()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "content");
        var input = CreateInput(
            ("file_path", "test.txt"),
            ("old_text", ""),
            ("new_text", "new")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error editing file:");
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
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("old_text", "old"),
            ("new_text", "new")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error editing file:");
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
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("file_path", "../../etc/test.txt"),
            ("old_text", "old"),
            ("new_text", "new")
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
        var tool = new EditFile
        {
            Options = new Agent.AgentOptions
            {
                AllowedExternalPaths = new List<string> { Path.GetTempPath() }
            }
        };
        var workspace = GetTestWorkspace();
        var tempFile = Path.Combine(Path.GetTempPath(), $"dracode-test-{Guid.NewGuid()}.txt");
        File.WriteAllText(tempFile, "Old content");
        var input = CreateInput(
            ("file_path", tempFile),
            ("old_text", "Old"),
            ("new_text", "New")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(tempFile).Should().Be("New content");
        }
        finally
        {
            CleanupWorkspace(workspace);
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Execute_WithNewLineInReplacement_ShouldPreserveStructure()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var content = "Line 1\nLine 2\nLine 3";
        CreateTestFile(workspace, "test.txt", content);
        var input = CreateInput(
            ("file_path", "test.txt"),
            ("old_text", "Line 2"),
            ("new_text", "Modified\nLine 2")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be("Line 1\nModified\nLine 2\nLine 3");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithSpecialCharacters_ShouldReplaceCorrectly()
    {
        // Arrange
        var tool = new EditFile();
        var workspace = GetTestWorkspace();
        var content = "Special: <>&\"'";
        CreateTestFile(workspace, "test.txt", content);
        var input = CreateInput(
            ("file_path", "test.txt"),
            ("old_text", "<>&\"'"),
            ("new_text", "[replaced]")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var filePath = Path.Combine(workspace, "test.txt");

            // Assert
            result.Should().Be("OK");
            File.ReadAllText(filePath).Should().Be("Special: [replaced]");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }
}
