using Akin.Core.Commands;
using Akin.Core.Models;
using Akin.Core.Services;
using Akin.Tests.Fakes;

namespace Akin.Tests
{
    public class StatusCommandTests : IAsyncLifetime
    {
        private string _indexFolder = string.Empty;
        private IndexStore _store = null!;

        public async Task InitializeAsync()
        {
            _indexFolder = Path.Combine(Path.GetTempPath(), "akin-status-" + Guid.NewGuid().ToString("N"));
            _store = new IndexStore(_indexFolder, dimension: 8);
            await _store.OpenAsync();
        }

        public async Task DisposeAsync()
        {
            await _store.DisposeAsync();
            try { Directory.Delete(_indexFolder, recursive: true); }
            catch { }
        }

        private StatusCommand BuildCommand(bool paused) =>
            new StatusCommand(_store, repoRoot: "/repo", _indexFolder, compatible: true, new AkinConfig(), new StubGlobalStateWatcher(paused));

        [Fact]
        public async Task ExecuteAsync_WhenPaused_ReportsPaused()
        {
            CommandResult result = await BuildCommand(paused: true).ExecuteAsync(CancellationToken.None);

            Assert.Contains("Indexing:", result.Details);
            Assert.Contains("paused", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_WhenActive_ReportsActive()
        {
            CommandResult result = await BuildCommand(paused: false).ExecuteAsync(CancellationToken.None);

            Assert.Contains("Indexing:", result.Details);
            Assert.Contains("active", result.Details);
            Assert.DoesNotContain("paused", result.Details);
        }
    }
}
