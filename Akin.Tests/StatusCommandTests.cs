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

        private StatusCommand BuildCommand(StubGlobalStateWatcher watcher) =>
            new StatusCommand(_store, repoRoot: "/repo", _indexFolder, compatible: true, new AkinConfig(), watcher);

        private StatusCommand BuildCommand(bool paused) =>
            BuildCommand(new StubGlobalStateWatcher(paused));

        [Fact]
        public async Task ExecuteAsync_WhenManuallyPaused_ReportsPaused()
        {
            CommandResult result = await BuildCommand(paused: true).ExecuteAsync(CancellationToken.None);

            Assert.Contains("Indexing:", result.Details);
            Assert.Contains("paused", result.Details);
            Assert.Contains("manual", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_WhenActive_ReportsActive()
        {
            CommandResult result = await BuildCommand(paused: false).ExecuteAsync(CancellationToken.None);

            Assert.Contains("Indexing:", result.Details);
            Assert.Contains("active", result.Details);
            Assert.DoesNotContain("paused", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_WhenOnBatteryWithAutoPauseOn_ReportsBatteryPause()
        {
            PauseState state = new PauseState(manuallyPaused: false, autoPauseEnabled: true, onBattery: true);

            CommandResult result = await BuildCommand(new StubGlobalStateWatcher(state)).ExecuteAsync(CancellationToken.None);

            Assert.Contains("paused", result.Details);
            Assert.Contains("battery", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_RendersAutoPauseLine()
        {
            CommandResult result = await BuildCommand(paused: false).ExecuteAsync(CancellationToken.None);

            Assert.Contains("Auto-pause:", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_ManualPauseShadowsBatteryReason()
        {
            // When the user has manually paused, that's the reason we should
            // show — auto-pause's status is still rendered but the indexing
            // line stays on the manual explanation so the resume hint points
            // at the right command.
            PauseState state = new PauseState(manuallyPaused: true, autoPauseEnabled: true, onBattery: true);

            CommandResult result = await BuildCommand(new StubGlobalStateWatcher(state)).ExecuteAsync(CancellationToken.None);

            Assert.Contains("manual", result.Details);
            Assert.Contains("akin resume", result.Details);
        }
    }
}
