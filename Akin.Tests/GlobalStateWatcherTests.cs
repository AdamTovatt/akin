using Akin.Core.Models;
using Akin.Core.Services;

namespace Akin.Tests
{
    public class GlobalStateWatcherTests : IDisposable
    {
        private readonly string _stateFolder;

        public GlobalStateWatcherTests()
        {
            _stateFolder = Path.Combine(Path.GetTempPath(), "akin-watcher-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try { Directory.Delete(_stateFolder, recursive: true); }
            catch { }
        }

        private Task WriteStateAsync(bool paused) =>
            new GlobalState { IndexingPaused = paused }.SaveAsync(_stateFolder);

        [Fact]
        public async Task IsPausedAsync_NoStateFile_ReturnsFalse()
        {
            GlobalStateWatcher watcher = new GlobalStateWatcher(_stateFolder);

            Assert.False(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_ReadsPausedFlagFromDisk()
        {
            await WriteStateAsync(paused: true);
            GlobalStateWatcher watcher = new GlobalStateWatcher(_stateFolder);

            Assert.True(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_WithinRefreshInterval_ServesCachedValue()
        {
            await WriteStateAsync(paused: true);
            // A long interval guarantees the second read is served from cache,
            // not re-read from disk, so the on-disk change is not yet observed.
            GlobalStateWatcher watcher = new GlobalStateWatcher(_stateFolder, TimeSpan.FromMinutes(5));
            Assert.True(await watcher.IsPausedAsync());

            await WriteStateAsync(paused: false);

            Assert.True(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_AfterRefreshInterval_ReReadsFromDisk()
        {
            await WriteStateAsync(paused: true);
            GlobalStateWatcher watcher = new GlobalStateWatcher(_stateFolder, TimeSpan.FromMilliseconds(50));
            Assert.True(await watcher.IsPausedAsync());

            await WriteStateAsync(paused: false);
            await Task.Delay(200);

            Assert.False(await watcher.IsPausedAsync());
        }
    }
}
