using DraCode.Agent.Tools;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DraCode.Agent.Tests.Tools;

public class AskUserTests : TestBase
{
    public AskUserTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Name_ShouldBe_ask_user()
    {
        // Arrange
        var tool = new AskUser();

        // Assert
        tool.Name.Should().Be("ask_user");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var tool = new AskUser();

        // Assert
        tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Execute_WithQuestion_ShouldSendPrompt()
    {
        // Arrange
        var tool = new AskUser();
        string? sentPrompt = null;
        tool.MessageCallback = (type, content) =>
        {
            if (type == "prompt_console") sentPrompt = content;
        };
        var workspace = GetTestWorkspace();
        var input = CreateInput(("question", "What is your name?"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert - Without callback, should return error about console input
            result.Should().StartWith("Error: Console input not available");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithPromptCallback_ShouldInvokeCallback()
    {
        // Arrange
        var tool = new AskUser
        {
            PromptCallback = (q, c) => Task.FromResult("User answer")
        };
        List<string> sentPrompts = new();
        tool.MessageCallback = (type, content) => sentPrompts.Add($"{type}:{content}");
        var workspace = GetTestWorkspace();
        var input = CreateInput(("question", "What is your name?"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("User answer");
            sentPrompts.Should().Contain(p => p.StartsWith("prompt:"));
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithPromptCallbackAndContext_ShouldIncludeBoth()
    {
        // Arrange
        var tool = new AskUser
        {
            PromptCallback = (q, c) => Task.FromResult("Answer")
        };
        string? sentPrompt = null;
        tool.MessageCallback = (type, content) => sentPrompt = content;
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("question", "Question?"),
            ("context", "Some context")
        );

        try
        {
            // Act
            tool.Execute(workspace, input);

            // Assert
            sentPrompt.Should().NotBeNull();
            sentPrompt!.Should().Contain("Context:");
            sentPrompt.Should().Contain("Some context");
            sentPrompt.Should().Contain("Question:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNonInteractiveModeAndDefaultResponse_ShouldReturnDefault()
    {
        // Arrange
        var tool = new AskUser
        {
            Options = new Agent.AgentOptions
            {
                Interactive = false,
                DefaultPromptResponse = "Default answer"
            }
        };
        List<string> messages = new();
        tool.MessageCallback = (type, content) => messages.Add($"{type}:{content}");
        var workspace = GetTestWorkspace();
        var input = CreateInput(("question", "What is your name?"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Default answer");
            messages.Should().Contain(m => m.Contains("Non-Interactive Mode"));
            messages.Should().Contain(m => m.Contains("Auto-responding"));
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNonInteractiveModeAndNoDefault_ShouldReturnError()
    {
        // Arrange
        var tool = new AskUser
        {
            Options = new Agent.AgentOptions
            {
                Interactive = false,
                DefaultPromptResponse = null
            }
        };
        List<string> messages = new();
        tool.MessageCallback = (type, content) => messages.Add($"{type}:{content}");
        var workspace = GetTestWorkspace();
        var input = CreateInput(("question", "What is your name?"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: Cannot prompt for user input in non-interactive mode");
            messages.Should().Contain(m => m.Contains("Non-Interactive Mode"));
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithEmptyQuestion_ShouldReturnError()
    {
        // Arrange
        var tool = new AskUser();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("question", ""));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: question parameter is required");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMissingQuestion_ShouldReturnError()
    {
        // Arrange
        var tool = new AskUser();
        var workspace = GetTestWorkspace();
        var input = CreateInput();

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error getting user input:");
            result.Should().Contain("question");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithPromptCallbackTimeout_ShouldTimeout()
    {
        // Arrange
        var tool = new AskUser
        {
            Options = new Agent.AgentOptions { PromptTimeout = 1 }, // 1 second timeout
            PromptCallback = (q, c) => Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => "Delayed answer")
        };
        List<string> warnings = new();
        tool.MessageCallback = (type, content) =>
        {
            if (type == "warning") warnings.Add(content);
        };
        var workspace = GetTestWorkspace();
        var input = CreateInput(("question", "Quick question"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: Prompt timed out after 1 seconds");
            warnings.Should().Contain(w => w.Contains("timed out"));
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithPromptCallback_ShouldPassQuestionAndContext()
    {
        // Arrange
        string? receivedQuestion = null;
        string? receivedContext = null;
        var tool = new AskUser
        {
            PromptCallback = (q, c) =>
            {
                receivedQuestion = q;
                receivedContext = c;
                return Task.FromResult("Response");
            }
        };
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("question", "Test Question"),
            ("context", "Test Context")
        );

        try
        {
            // Act
            tool.Execute(workspace, input);

            // Assert
            receivedQuestion.Should().Be("Test Question");
            receivedContext.Should().Be("Test Context");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithoutPromptCallback_ShouldSendConsolePrompt()
    {
        // Arrange
        var tool = new AskUser(); // No PromptCallback set
        string? sentPrompt = null;
        tool.MessageCallback = (type, content) =>
        {
            if (type == "prompt_console") sentPrompt = content;
        };
        var workspace = GetTestWorkspace();
        var input = CreateInput(
            ("question", "Question"),
            ("context", "Context")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error: Console input not available");
            sentPrompt.Should().NotBeNull();
            sentPrompt!.Should().Contain("Context:");
            sentPrompt.Should().Contain("Question:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithInteractiveTrue_ShouldUsePromptCallback()
    {
        // Arrange
        var tool = new AskUser
        {
            Options = new Agent.AgentOptions { Interactive = true },
            PromptCallback = (q, c) => Task.FromResult("Interactive response")
        };
        var workspace = GetTestWorkspace();
        var input = CreateInput(("question", "Question?"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Interactive response");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNullContext_ShouldHandleGracefully()
    {
        // Arrange
        string? receivedContext = null;
        var tool = new AskUser
        {
            PromptCallback = (q, c) =>
            {
                receivedContext = c;
                return Task.FromResult("Response");
            }
        };
        var workspace = GetTestWorkspace();
        var input = CreateInput(("question", "Question?"), ("context", null!));

        try
        {
            // Act
            tool.Execute(workspace, input);

            // Assert
            receivedContext.Should().NotBeNull(); // May be empty string or null
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }
}
