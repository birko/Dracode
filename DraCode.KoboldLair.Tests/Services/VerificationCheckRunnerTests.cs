using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// TASK-179: a verification check runs where the project's code is, under a POSIX shell, so a project whose fixes made
/// it pass is not failed again by the check itself.
/// </summary>
public class VerificationCheckRunnerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_checks_{Guid.NewGuid():N}");

    public VerificationCheckRunnerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private Project NewProject()
    {
        var folder = Path.Combine(_dir, "waver");
        Directory.CreateDirectory(Path.Combine(folder, "workspace"));
        return new Project { Name = "waver", Paths = new ProjectPaths { Output = folder } };
    }

    private static VerificationStepDefinition Step(string command, string criteria = "exit_code_0", string? workingDirectory = null) => new()
    {
        CheckType = "test",
        Command = command,
        Priority = "Critical",
        SuccessCriteria = criteria,
        WorkingDirectory = workingDirectory,
        TimeoutSeconds = 60
    };

    private static bool PosixShell => !VerificationShell.Describe().StartsWith("powershell", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task A_check_runs_in_the_projects_workspace()
    {
        var project = NewProject();
        await File.WriteAllTextAsync(Path.Combine(project.Paths.Output, "workspace", "waver.js"), "exports.greet = n => `Hello, ${n}!`;\n");

        var check = await VerificationCheckRunner.RunAsync(project, Step("cat waver.js", "contains:exports.greet"), 60, CancellationToken.None);

        check.Passed.Should().BeTrue(check.Output);
    }

    [Fact]
    public async Task A_steps_working_directory_is_relative_to_the_workspace()
    {
        var project = NewProject();
        var lib = Path.Combine(project.Paths.Output, "workspace", "lib");
        Directory.CreateDirectory(lib);
        await File.WriteAllTextAsync(Path.Combine(lib, "util.js"), "module.exports = {};\n");

        var check = await VerificationCheckRunner.RunAsync(project, Step("cat util.js", "contains:module.exports", "lib"), 60, CancellationToken.None);

        check.Passed.Should().BeTrue(check.Output);
    }

    [Fact]
    public async Task An_imported_project_is_checked_in_its_source_path()
    {
        var project = NewProject();
        var source = Path.Combine(_dir, "external-repo");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "marker.txt"), "external source\n");
        project.Metadata["IsExistingProject"] = "true";
        project.Metadata["SourcePath"] = source;

        var check = await VerificationCheckRunner.RunAsync(project, Step("cat marker.txt", "contains:external source"), 60, CancellationToken.None);

        check.Passed.Should().BeTrue(check.Output);
    }

    [Fact]
    public async Task A_missing_workspace_fails_the_check_with_a_message()
    {
        var project = new Project { Name = "empty", Paths = new ProjectPaths { Output = Path.Combine(_dir, "nothing") } };

        var check = await VerificationCheckRunner.RunAsync(project, Step("echo hi"), 60, CancellationToken.None);

        check.Passed.Should().BeFalse();
        check.Output.Should().Contain("Working directory not found");
    }

    [Fact]
    public async Task A_POSIX_command_is_evaluated_by_a_POSIX_shell()
    {
        if (!PosixShell)
            return; // a Windows machine without Git bash falls back to PowerShell, which this step is not written for

        var project = NewProject();
        var check = await VerificationCheckRunner.RunAsync(project,
            Step("false && echo ran || echo fallback; x=\"a b\"; echo \"[$x]\" '[$x]'"), 60, CancellationToken.None);

        check.Passed.Should().BeTrue(check.Output);
        check.Output.Should().Contain("fallback").And.NotContain("ran\n");
        check.Output.Should().Contain("[a b] [$x]", "the command reaches the shell as one argument, quotes and $ intact");
    }

    [Fact]
    public async Task A_failing_command_fails_the_check_with_its_output()
    {
        var project = NewProject();

        var check = await VerificationCheckRunner.RunAsync(project, Step("cat no-such-file.js"), 60, CancellationToken.None);

        check.Passed.Should().BeFalse();
        check.ExitCode.Should().NotBe(0);
        check.Output.Should().Contain("no-such-file.js");
    }
}
