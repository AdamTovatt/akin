using System.ComponentModel;
using System.Diagnostics;
using Akin.Core.Interfaces;

namespace Akin.Core.Services
{
    /// <summary>
    /// Enumerates files in a repository by shelling out to <c>git ls-files</c>.
    /// This automatically respects <c>.gitignore</c> and matches the exact set of
    /// files the user has chosen to version.
    /// </summary>
    public sealed class RepoScanner : IRepoScanner
    {
        private readonly string _repoRoot;

        public RepoScanner(string repoRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
            _repoRoot = repoRoot;
        }

        public async Task<IReadOnlyList<string>> ScanAsync(CancellationToken cancellationToken = default)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = _repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("ls-files");
            startInfo.ArgumentList.Add("-z");

            Process process;
            try
            {
                process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Failed to start git process.");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not launch 'git'. Make sure git is installed and available on PATH.",
                    ex);
            }

            using (process)
            {
                string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                string error = await process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"git ls-files failed with exit code {process.ExitCode}: {error}");
                }

                List<string> files = new List<string>();
                foreach (string entry in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
                {
                    string normalized = entry.Replace('\\', '/');
                    files.Add(normalized);
                }

                files.Sort(StringComparer.Ordinal);
                return files;
            }
        }
    }
}
