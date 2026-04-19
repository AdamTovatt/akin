using Akin.Core.Services;

namespace Akin.Tests
{
    public class RepoLocatorTests : IDisposable
    {
        private readonly string _tempRoot;

        public RepoLocatorTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "akin-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures on Windows shares / permission quirks.
            }
        }

        [Fact]
        public void FindRepoRoot_FromRootWithGitDir_ReturnsRoot()
        {
            Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
            string? found = RepoLocator.FindRepoRoot(_tempRoot);
            Assert.Equal(_tempRoot, found);
        }

        [Fact]
        public void FindRepoRoot_FromNestedDirectory_WalksUp()
        {
            Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
            string nested = Path.Combine(_tempRoot, "src", "sub", "deep");
            Directory.CreateDirectory(nested);

            string? found = RepoLocator.FindRepoRoot(nested);
            Assert.Equal(_tempRoot, found);
        }

        [Fact]
        public void FindRepoRoot_WithGitFile_Worktree_ReturnsRoot()
        {
            // Worktrees use a .git file rather than a directory; the locator should
            // still recognise them as repo roots.
            File.WriteAllText(Path.Combine(_tempRoot, ".git"), "gitdir: /some/place");
            string? found = RepoLocator.FindRepoRoot(_tempRoot);
            Assert.Equal(_tempRoot, found);
        }

        [Fact]
        public void FindRepoRoot_NoGit_ReturnsNull()
        {
            string? found = RepoLocator.FindRepoRoot(_tempRoot);
            Assert.Null(found);
        }
    }
}
