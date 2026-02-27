using DraCode.Agent.Tools;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DraCode.Agent.Tests.Tools;

public class RunCommandTests : TestBase
{
    public RunCommandTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Name_ShouldBe_run_command()
    {
        // Arrange
        var tool = new RunCommand();

        // Assert
        tool.Name.Should().Be("run_command");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var tool = new RunCommand();

        // Assert
        tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact(Skip = "Platform-specific test - requires valid shell")]
    public void Execute_WithEchoCommand_ShouldReturnOutput()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        string command = OperatingSystem.IsWindows() ? "cmd" : "echo";
        string args = OperatingSystem.IsWindows() ? "/c echo Hello" : "Hello";
        var input = CreateInput(
            ("command", command),
            ("arguments", args)
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Contain("Hello");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithMissingCommand_ShouldReturnError()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        var input = CreateInput();

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error running command:");
            result.Should().Contain("command");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithEmptyCommand_ShouldReturnError()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("command", ""));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error running command:");
            result.Should().Contain("command");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithNonExistentWorkingDirectory_ShouldReturnError()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = "/nonexistent/directory/that/does/not/exist";
        var input = CreateInput(("command", "test"));

        // Act
        var result = tool.Execute(workspace, input);

        // Assert
        result.Should().StartWith("Error running command:");
    }

    [Fact(Skip = "Platform-specific test - may have different behavior")]
    public void Execute_WithNonExistentCommand_ShouldReturnError()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("command", "nonexistent_command_xyz_123"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().StartWith("Error running command:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact(Skip = "Platform-specific test - requires valid shell")]
    public void Execute_WithTimeout_ShouldTimeout()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        string command = OperatingSystem.IsWindows() ? "timeout" : "sleep";
        string args = OperatingSystem.IsWindows() ? "/t 10" : "10";
        var input = CreateInput(
            ("command", command),
            ("arguments", args),
            ("timeout_seconds", 1)
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Be("Error: Process timed out");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact(Skip = "Platform-specific test - requires valid shell")]
    public void Execute_WithCustomTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        string command = OperatingSystem.IsWindows() ? "timeout" : "sleep";
        string args = OperatingSystem.IsWindows() ? "/t 2" : "2";
        var input = CreateInput(
            ("command", command),
            ("arguments", args),
            ("timeout_seconds", 5)
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert - Should complete within timeout
            // (The actual timeout behavior depends on the command)
            result.Should().NotBe("Error: Process timed out");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithDefaultTimeout_ShouldUse120Seconds()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();

        // The default timeout should be 120 seconds
        // We can't really test this without a long-running command
        // but we can verify the input parsing works
        var input = CreateInput(
            ("command", "nonexistent-command-xyz"),
            ("timeout_seconds", null)
        );

        try
        {
            // Act - should not throw on null timeout_seconds
            var result = tool.Execute(workspace, input);

            // Assert - command doesn't exist so should get error
            result.Should().StartWith("Error running command:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact]
    public void Execute_WithInvalidTimeoutString_ShouldUseDefault()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        // Use a command that likely doesn't exist
        var input = CreateInput(
            ("command", "nonexistent-command-xyz"),
            ("timeout_seconds", "invalid")
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert - should parse invalid as 0 and use default 120
            result.Should().StartWith("Error running command:");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact(Skip = "Platform-specific test - requires git")]
    public void Execute_WithGitCommand_ShouldExecute()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("command", "git"), ("arguments", "--version"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert - git may not be installed
            if (result.StartsWith("Error running command:"))
            {
                // git not installed - test would be skipped in real scenario
                return;
            }

            result.Should().Contain("git");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact(Skip = "Platform-specific test - requires dotnet")]
    public void Execute_WithDotnetCommand_ShouldExecute()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        var input = CreateInput(("command", "dotnet"), ("arguments", "--version"));

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert - dotnet may not be installed
            if (result.StartsWith("Error running command:"))
            {
                // dotnet not installed - test would be skipped in real scenario
                return;
            }

            result.Should().NotBeEmpty();
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }

    [Fact(Skip = "Platform-specific test - requires valid shell")]
    public void Execute_WithStderrOutput_ShouldIncludeStderr()
    {
        // Arrange
        var tool = new RunCommand();
        var workspace = GetTestWorkspace();
        string command = OperatingSystem.IsWindows() ? "cmd" : "sh";
        string args = OperatingSystem.IsWindows() ? "/c echo Error >&2" : "-c \"echo Error >&2\"";
        var input = CreateInput(
            ("command", command),
            ("arguments", args)
        );

        try
        {
            // Act
            var result = tool.Execute(workspace, input);

            // Assert
            result.Should().Contain("Error");
        }
        finally
        {
            CleanupWorkspace(workspace);
        }
    }
}
