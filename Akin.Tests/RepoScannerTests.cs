using System.Diagnostics;
using Akin.Core.Services;

namespace Akin.Tests
{
    /// <summary>
    /// Integration tests for <see cref="RepoScanner"/>. Each test creates a throwaway
    /// git repository in a temp folder, stages files, and runs the scanner against
    /// the real git executable.
    /// </summary>
    public class RepoScannerTests : IDisposable
    {
        private readonly string _tempRepo;

        public RepoScannerTests()
        {
            _tempRepo = Path.Combine(Path.GetTempPath(), "akin-scanner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRepo);
            RunGit("init", "-q");
            RunGit("config", "user.email", "test@example.com");
            RunGit("config", "user.name", "test");
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempRepo, recursive: true); } catch { }
        }

        private void RunGit(params string[] args)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = _tempRepo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string arg in args) info.ArgumentList.Add(arg);
            using Process proc = Process.Start(info)!;
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                string stderr = proc.StandardError.ReadToEnd();
                throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr}");
            }
        }

        private void WriteFile(string relativePath, string content)
        {
            string full = Path.Combine(_tempRepo, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        [Fact]
        public async Task ScanAsync_ReturnsStagedFiles()
        {
            WriteFile("a.cs", "class A {}");
            WriteFile("b.md", "# hi");
            RunGit("add", ".");

            RepoScanner scanner = new RepoScanner(_tempRepo);
            IReadOnlyList<string> files = await scanner.ScanAsync();

            Assert.Contains("a.cs", files);
            Assert.Contains("b.md", files);
        }

        [Fact]
        public async Task ScanAsync_RespectsGitignore()
        {
            WriteFile(".gitignore", "ignored.txt\n");
            WriteFile("kept.cs", "class K {}");
            WriteFile("ignored.txt", "don't index me");
            RunGit("add", ".");

            RepoScanner scanner = new RepoScanner(_tempRepo);
            IReadOnlyList<string> files = await scanner.ScanAsync();

            Assert.Contains("kept.cs", files);
            Assert.DoesNotContain("ignored.txt", files);
        }

        [Fact]
        public async Task ScanAsync_NormalizesPathSeparators()
        {
            WriteFile(Path.Combine("src", "nested", "Foo.cs"), "class Foo {}");
            RunGit("add", ".");

            RepoScanner scanner = new RepoScanner(_tempRepo);
            IReadOnlyList<string> files = await scanner.ScanAsync();

            Assert.Contains("src/nested/Foo.cs", files);
        }
    }
}
