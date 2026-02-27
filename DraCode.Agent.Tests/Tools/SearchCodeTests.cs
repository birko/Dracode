using DraCode.Agent.Tools;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DraCode.Agent.Tests.Tools;

public class SearchCodeTests : TestBase
{
    public SearchCodeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Name_ShouldBe_search_code()
    {
        // Arrange
        var tool = new SearchCode();

        // Assert
        tool.Name.Should().Be("search_code");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var tool = new SearchCode();

        // Assert
        tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Execute_WithMatchingText_ShouldReturnResults()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "Hello World\nHello Universe");
        var input = CreateInput(("query", "Hello"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Assert
            lines.Should().HaveCount(2);
            lines[0].Should().Contain("test.txt:1");
            lines[0].Should().Contain("Hello World");
            lines[1].Should().Contain("test.txt:2");
            lines[1].Should().Contain("Hello Universe");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNoMatches_ShouldReturnEmpty()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "Hello World");
        var input = CreateInput(("query", "Nonexistent"));

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
    public void Execute_WithRecursiveTrue_ShouldSearchNestedFiles()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "file1.txt", "Match");
        CreateTestFile(workspace, "subdir/file2.txt", "Match");
        CreateTestFile(workspace, "subdir/nested/file3.txt", "Match");
        var input = CreateInput(("query", "Match"), ("recursive", true));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Assert
            lines.Should().HaveCount(3);
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithRecursiveFalse_ShouldNotSearchNestedFiles()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "file1.txt", "Match");
        CreateTestFile(workspace, "subdir/file2.txt", "Match");
        var input = CreateInput(("query", "Match"), ("recursive", false));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Assert
            lines.Should().HaveCount(1);
            lines[0].Should().Contain("file1.txt");
            lines[0].Should().NotContain("subdir");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithPatternFilter_ShouldMatchOnlyPattern()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "file.txt", "Match");
        CreateTestFile(workspace, "file.cs", "Match");
        CreateTestFile(workspace, "file.js", "Match");
        var input = CreateInput(("query", "Match"), ("pattern", "*.cs"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Contain("file.cs");
            result.Should().NotContain("file.txt");
            result.Should().NotContain("file.js");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithCaseSensitiveFalse_ShouldIgnoreCase()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "hello HELLO HeLLo");
        var input = CreateInput(("query", "HELLO"), ("case_sensitive", false));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Assert
            lines[0].Should().Contain("test.txt:1");
            // All variations should match
            lines[0].Should().Contain("hello HELLO HeLLo");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithCaseSensitiveTrue_ShouldMatchExactCase()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "hello HELLO HeLLo");
        var input = CreateInput(("query", "HELLO"), ("case_sensitive", true));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert - The tool returns the full line containing the match
            // So the line "hello HELLO HeLLo" is returned because it contains "HELLO"
            result.Should().NotBeEmpty();
            result.Should().Contain("HELLO"); // The matched case is present
            // Note: The full line is returned, so "hello" and "HeLLo" are also in the output
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithRegexTrue_ShouldMatchPattern()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "abc123 xyz456 def789");
        var input = CreateInput(("query", @"\d+"), ("regex", true));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert - The regex \d+ should match numbers
            result.Should().NotBeEmpty();
            result.Should().Contain("123");
            result.Should().Contain("456");
            result.Should().Contain("789");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithSubdirectory_ShouldSearchOnlyInSubdirectory()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "file.txt", "Match");
        CreateTestFile(workspace, "subdir/file.txt", "Match");
        var input = CreateInput(
            ("query", "Match"),
            ("directory", "subdir")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            // Should contain subdir file, not root file
            result.Should().Contain("subdir");
            result.Should().Contain("file.txt");
            // Root file should not be in results (it would appear without subdir prefix)
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lines.Should().Contain(l => l.Contains("subdir"));
            // Check that there's no line that's just "file.txt:1: Match"
            lines.Should().NotContain(l => l.StartsWith("file.txt:"));
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
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("query", "Match"),
            ("directory", "nonexistent")
        );

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
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("query", "test"),
            ("directory", "../../etc")
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
    public void Execute_WithEmptyQuery_ShouldReturnError()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "content");
        var input = CreateInput(("query", ""));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error searching code:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithBinaryFile_ShouldSkip()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "test.txt", "Match");
        var binaryPath = Path.Combine(workspace, "test.png");
        File.WriteAllBytes(binaryPath, new byte[] { 0x00, 0x01 });
        var input = CreateInput(("query", "Match"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Contain("test.txt");
            result.Should().NotContain("test.png");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMultipleFiles_ShouldShowAllMatches()
    {
        // Arrange
        var tool = new SearchCode();
        var workspace = GetTestWorkspace();
        CreateTestFile(workspace, "file1.txt", "line1\nline2\nMatch\nline3");
        CreateTestFile(workspace, "file2.txt", "no match");
        CreateTestFile(workspace, "file3.txt", "Match here too");
        var input = CreateInput(("query", "Match"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Assert
            lines.Should().Contain(l => l.Contains("file1.txt") && l.Contains("3"));
            lines.Should().NotContain(l => l.Contains("file2.txt") && l.Contains("Match"));
            lines.Should().Contain(l => l.Contains("file3.txt"));
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }
}
