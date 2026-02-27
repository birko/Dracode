using DraCode.Agent.Tools;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DraCode.Agent.Tests.Tools;

public class ReadFileTests : TestBase
{
    public ReadFileTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Name_ShouldBe_read_file()
    {
        // Arrange
        var tool = new ReadFile();

        // Assert
        tool.Name.Should().Be("read_file");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var tool = new ReadFile();

        // Assert
        tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Execute_WithExistingFile_ShouldReturnContent()
    {
        // Arrange
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var content = "Hello, World!";
        CreateTestFile(workspace, "test.txt", content);
        var input = CreateInput(("file_path", "test.txt"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be(content);
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNestedFile_ShouldReturnContent()
    {
        // Arrange
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var content = "Nested content";
        CreateTestFile(workspace, "subdir/nested.txt", content);
        var input = CreateInput(("file_path", "subdir/nested.txt"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be(content);
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
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("file_path", "nonexistent.txt"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error reading file:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithEmptyFile_ShouldReturnEmptyString()
    {
        // Arrange
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "empty.txt", "");
        var input = CreateInput(("file_path", "empty.txt"));

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
    public void Execute_WithMultilineContent_ShouldReturnFullContent()
    {
        // Arrange
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var content = "Line 1\nLine 2\nLine 3";
        CreateTestFile(workspace, "multiline.txt", content);
        var input = CreateInput(("file_path", "multiline.txt"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be(content);
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithSpecialCharacters_ShouldReturnContent()
    {
        // Arrange
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var content = "Special chars: <>&\"'\\t\\n";
        CreateTestFile(workspace, "special.txt", content);
        var input = CreateInput(("file_path", "special.txt"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be(content);
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithBinaryFile_ShouldReturnContent()
    {
        // Arrange
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var binaryContent = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF };
        var binaryPath = Path.Combine(workspace, "binary.bin");
        File.WriteAllBytes(binaryPath, binaryContent);
        var input = CreateInput(("file_path", "binary.bin"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert - Should read binary content as string (may contain null chars)
            result.Should().NotBeEmpty();
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
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("file_path", "../../etc/passwd"));

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
    public void Execute_WithMissingFilePath_ShouldReturnError()
    {
        // Arrange
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var input = CreateInput();

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error reading file:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithLargeFile_ShouldReturnContent()
    {
        // Arrange
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var largeContent = new string('A', 100_000);
        CreateTestFile(workspace, "large.txt", largeContent);
        var input = CreateInput(("file_path", "large.txt"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().HaveLength(100_000);
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
        var tool = new ReadFile
        {
            Options = new Agent.AgentOptions
            {
                AllowedExternalPaths = new List<string> { Path.GetTempPath() }
            }
        };
        var workspace = GetTestWorkspace();
        var tempFile = Path.Combine(Path.GetTempPath(), $"dracode-test-{Guid.NewGuid()}.txt");
        File.WriteAllText(tempFile, "external content");
        var input = CreateInput(("file_path", tempFile));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("external content");
        }
        finally
        {
            CleanupWorkspace(workspace);
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Execute_WithRelativePath_ShouldResolveCorrectly()
    {
        // Arrange
        var tool = new ReadFile();
        var workspace = GetTestWorkspace();
        var content = "Relative path test";
        CreateTestFile(workspace, "test.txt", content);
        var input = CreateInput(("file_path", "./test.txt"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be(content);
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }
}
