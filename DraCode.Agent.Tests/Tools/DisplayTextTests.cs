using DraCode.Agent.Tools;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DraCode.Agent.Tests.Tools;

public class DisplayTextTests : TestBase
{
    private readonly List<string> _displayedMessages;

    public DisplayTextTests(ITestOutputHelper output) : base(output)
    {
        _displayedMessages = new List<string>();
    }

    [Fact]
    public void Name_ShouldBe_display_text()
    {
        // Arrange
        var tool = new DisplayText();

        // Assert
        tool.Name.Should().Be("display_text");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var tool = new DisplayText();

        // Assert
        tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Execute_WithText_ShouldSendMessage()
    {
        // Arrange
        var tool = new DisplayText();
        var messages = new List<string>();
        tool.MessageCallback = (type, content) => messages.Add((type, content).ToString());
        var workspace = GetTestWorkspace();
        var input = CreateInput(("text", "Hello, World!"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Text displayed successfully");
            messages.Should().HaveCount(1);
            messages[0].Should().Contain("display");
            messages[0].Should().Contain("Hello, World!");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithTitle_ShouldIncludeTitleInMessage()
    {
        // Arrange
        var tool = new DisplayText();
        string? capturedContent = null;
        tool.MessageCallback = (type, content) => capturedContent = content;
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("text", "Content here"),
            ("title", "=== My Title ===")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Text displayed successfully");
            capturedContent.Should().NotBeNull();
            capturedContent!.Should().Contain("=== My Title ===");
            capturedContent.Should().Contain("Content here");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMultilineText_ShouldPreserveFormatting()
    {
        // Arrange
        var tool = new DisplayText();
        string? capturedContent = null;
        tool.MessageCallback = (type, content) => capturedContent = content;
        var workspace = GetTestWorkspace();
        var multilineText = "Line 1\nLine 2\nLine 3";
        var input = CreateInput(("text", multilineText));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Text displayed successfully");
            capturedContent.Should().Contain("Line 1");
            capturedContent.Should().Contain("Line 2");
            capturedContent.Should().Contain("Line 3");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithEmptyText_ShouldReturnError()
    {
        // Arrange
        var tool = new DisplayText();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("text", ""));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: text parameter is required");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithWhitespaceOnlyText_ShouldReturnError()
    {
        // Arrange
        var tool = new DisplayText();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("text", "   "));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: text parameter is required");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMissingText_ShouldReturnError()
    {
        // Arrange
        var tool = new DisplayText();
        var workspace = GetTestWorkspace();
        var input = CreateInput();

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error displaying text:");
            result.Should().Contain("text");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithSpecialCharacters_ShouldDisplayCorrectly()
    {
        // Arrange
        var tool = new DisplayText();
        string? capturedContent = null;
        tool.MessageCallback = (type, content) => capturedContent = content;
        var workspace = GetTestWorkspace();
        var specialText = "Special: <>&\"'\\t\\n";
        var input = CreateInput(("text", specialText));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Text displayed successfully");
            capturedContent.Should().Contain("Special:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNullTitle_ShouldDisplayTextOnly()
    {
        // Arrange
        var tool = new DisplayText();
        string? capturedContent = null;
        tool.MessageCallback = (type, content) => capturedContent = content;
        var workspace = GetTestWorkspace();
        var input = CreateInput(("text", "Just text"), ("title", null!));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Text displayed successfully");
            capturedContent.Should().Be("Just text");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithEmptyTitle_ShouldDisplayTextOnly()
    {
        // Arrange
        var tool = new DisplayText();
        string? capturedContent = null;
        tool.MessageCallback = (type, content) => capturedContent = content;
        var workspace = GetTestWorkspace();
        var input = CreateInput(("text", "Just text"), ("title", ""));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Text displayed successfully");
            capturedContent.Should().Be("Just text");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithoutMessageCallback_ShouldNotThrow()
    {
        // Arrange
        var tool = new DisplayText(); // No callback set
        var workspace = GetTestWorkspace();
        var input = CreateInput(("text", "Test"));

        try
        {
            // Act & Assert - should not throw
            var result = tool.Execute(workspace, input);
            result.Should().Be("Text displayed successfully");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_MessageType_ShouldBe_display()
    {
        // Arrange
        var tool = new DisplayText();
        string? capturedType = null;
        tool.MessageCallback = (type, content) => capturedType = type;
        var workspace = GetTestWorkspace();
        var input = CreateInput(("text", "Test"));

        try
        {
            // Act
            tool.Execute(workspace, input);

            // Assert
            capturedType.Should().Be("display");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithVeryLongText_ShouldHandleGracefully()
    {
        // Arrange
        var tool = new DisplayText();
        string? capturedContent = null;
        tool.MessageCallback = (type, content) => capturedContent = content;
        var workspace = GetTestWorkspace();
        var longText = new string('A', 100_000);
        var input = CreateInput(("text", longText));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Text displayed successfully");
            capturedContent.Should().HaveLength(100_000);
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }
}
