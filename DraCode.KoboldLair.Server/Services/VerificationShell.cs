using System.Diagnostics;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// How a verification step's command is started (TASK-179). Steps are written for a POSIX shell (Wyrm's and the
    /// auto-detected ones use <c>&amp;&amp;</c>, <c>||</c>, <c>grep</c>), so they run under bash everywhere: <c>/bin/bash</c>
    /// off Windows, Git for Windows' bash on Windows. Only a Windows machine with no Git bash falls back to PowerShell.
    /// The command is passed as one argument, never spliced into a quoted string.
    /// </summary>
    public static class VerificationShell
    {
        private static readonly Lazy<string?> WindowsBash = new(FindWindowsBash);

        public static ProcessStartInfo CreateStartInfo(string command, string workingDirectory)
        {
            var info = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var bash = OperatingSystem.IsWindows() ? WindowsBash.Value : "/bin/bash";
            if (bash != null)
            {
                info.FileName = bash;
                info.ArgumentList.Add("-c");
                info.ArgumentList.Add(command);
            }
            else
            {
                info.FileName = "powershell.exe";
                info.ArgumentList.Add("-NoProfile");
                info.ArgumentList.Add("-Command");
                info.ArgumentList.Add(command);
            }
            return info;
        }

        /// <summary>The shell a step runs under on this machine, for the report and the log.</summary>
        public static string Describe() =>
            OperatingSystem.IsWindows() ? WindowsBash.Value ?? "powershell.exe (no Git bash found)" : "/bin/bash";

        /// <summary>
        /// Git for Windows' <c>bin\bash.exe</c>, found from <c>git.exe</c> on PATH (in <c>cmd\</c>, <c>bin\</c> or
        /// <c>mingw64\bin\</c> of the install) or in the default install folders. Never <c>System32\bash.exe</c>, which
        /// is WSL's and runs the command in another file system.
        /// </summary>
        private static string? FindWindowsBash()
        {
            var roots = new List<string>();
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string git;
                try { git = Path.Combine(dir.Trim().Trim('"'), "git.exe"); }
                catch (ArgumentException) { continue; }
                if (!File.Exists(git))
                    continue;
                for (var root = Path.GetDirectoryName(git); root != null; root = Path.GetDirectoryName(root))
                {
                    roots.Add(root);
                    if (roots.Count > 16) break;
                }
            }
            foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                var programFiles = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(programFiles))
                    roots.Add(Path.Combine(programFiles, "Git"));
            }

            return roots
                .Select(r => Path.Combine(r, "bin", "bash.exe"))
                .FirstOrDefault(File.Exists);
        }
    }
}
