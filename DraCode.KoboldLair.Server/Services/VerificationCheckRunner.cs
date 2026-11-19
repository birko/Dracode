using System.Diagnostics;
using System.Text;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Runs one verification step against a project (TASK-179): in the project's code folder
    /// (<see cref="ProjectWorkspace"/>, plus the step's <c>WorkingDirectory</c>), under the shell
    /// <see cref="VerificationShell"/> picks.
    /// </summary>
    public static class VerificationCheckRunner
    {
        public static async Task<VerificationCheck> RunAsync(
            Project project, VerificationStepDefinition step, int defaultTimeoutSeconds, CancellationToken stoppingToken)
        {
            var check = new VerificationCheck
            {
                CheckType = step.CheckType,
                Command = step.Command,
                Priority = Enum.TryParse<VerificationCheckPriority>(step.Priority, out var priority)
                    ? priority
                    : VerificationCheckPriority.Medium,
                ExecutedAt = DateTime.UtcNow
            };

            var workspace = ProjectWorkspace.For(project);
            var workingDir = string.IsNullOrEmpty(step.WorkingDirectory)
                ? workspace
                : Path.Combine(workspace, step.WorkingDirectory);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (!Directory.Exists(workingDir))
                    throw new DirectoryNotFoundException($"Working directory not found: {workingDir}");

                var timeout = step.TimeoutSeconds > 0 ? step.TimeoutSeconds : defaultTimeoutSeconds;
                var (exitCode, output) = await RunCommandAsync(step.Command, workingDir, timeout, stoppingToken);

                check.ExitCode = exitCode;
                check.Output = output;
                check.Passed = EvaluateSuccessCriteria(step.SuccessCriteria, exitCode, output);
            }
            catch (Exception ex)
            {
                check.ExitCode = -1;
                check.Output = $"Error executing check: {ex.Message}";
                check.Passed = false;
            }
            finally
            {
                stopwatch.Stop();
                check.DurationSeconds = stopwatch.Elapsed.TotalSeconds;
            }

            return check;
        }

        private static async Task<(int exitCode, string output)> RunCommandAsync(
            string command, string workingDir, int timeoutSeconds, CancellationToken stoppingToken)
        {
            using var process = new Process { StartInfo = VerificationShell.CreateStartInfo(command, workingDir) };

            var output = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            if (timeoutSeconds > 0)
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    await process.WaitForExitAsync(CancellationToken.None);
                    lock (output) output.AppendLine($"\n[TIMEOUT: Process killed after {timeoutSeconds}s]");
                }
            }

            lock (output)
                return (process.ExitCode, output.ToString());
        }

        private static bool EvaluateSuccessCriteria(string criteria, int exitCode, string output)
        {
            if (criteria == "exit_code_0")
                return exitCode == 0;

            if (criteria.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
                return output.Contains(criteria["contains:".Length..], StringComparison.OrdinalIgnoreCase);

            if (criteria.StartsWith("not_contains:", StringComparison.OrdinalIgnoreCase))
                return !output.Contains(criteria["not_contains:".Length..], StringComparison.OrdinalIgnoreCase);

            return exitCode == 0;
        }
    }
}
