using Akin.Core.Models;
using Akin.Core.Services;
using Akin.Tests.Fakes;

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

        private Task WriteStateAsync(bool paused, bool autoPause = true) =>
            new GlobalState { IndexingPaused = paused, AutoPauseOnBattery = autoPause }.SaveAsync(_stateFolder);

        private GlobalStateWatcher NewWatcher(FakePowerSourceProvider power, TimeSpan? refresh = null) =>
            new GlobalStateWatcher(_stateFolder, power, refresh);

        [Fact]
        public async Task IsPausedAsync_NoStateFile_ReturnsFalse()
        {
            GlobalStateWatcher watcher = NewWatcher(new FakePowerSourceProvider());

            Assert.False(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_ReadsPausedFlagFromDisk()
        {
            await WriteStateAsync(paused: true);
            GlobalStateWatcher watcher = NewWatcher(new FakePowerSourceProvider());

            Assert.True(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_WithinRefreshInterval_ServesCachedValue()
        {
            await WriteStateAsync(paused: true);
            // A long interval guarantees the second read is served from cache,
            // not re-read from disk, so the on-disk change is not yet observed.
            GlobalStateWatcher watcher = NewWatcher(new FakePowerSourceProvider(), TimeSpan.FromMinutes(5));
            Assert.True(await watcher.IsPausedAsync());

            await WriteStateAsync(paused: false);

            Assert.True(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_WhenCacheExpired_ReReadsFromDisk()
        {
            // A zero refresh interval makes every read stale, so the watcher
            // always re-reads from disk. This exercises the cache-expiry path
            // deterministically, without depending on wall-clock timing.
            await WriteStateAsync(paused: true);
            GlobalStateWatcher watcher = NewWatcher(new FakePowerSourceProvider(), TimeSpan.Zero);
            Assert.True(await watcher.IsPausedAsync());

            await WriteStateAsync(paused: false);

            Assert.False(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_AutoPauseOnAndOnBattery_ReportsPaused()
        {
            await WriteStateAsync(paused: false, autoPause: true);
            FakePowerSourceProvider power = new FakePowerSourceProvider { OnBattery = true };
            GlobalStateWatcher watcher = NewWatcher(power);

            Assert.True(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_AutoPauseOnAndOnAc_ReportsActive()
        {
            await WriteStateAsync(paused: false, autoPause: true);
            FakePowerSourceProvider power = new FakePowerSourceProvider { OnBattery = false };
            GlobalStateWatcher watcher = NewWatcher(power);

            Assert.False(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_AutoPauseOffAndOnBattery_ReportsActive()
        {
            await WriteStateAsync(paused: false, autoPause: false);
            FakePowerSourceProvider power = new FakePowerSourceProvider { OnBattery = true };
            GlobalStateWatcher watcher = NewWatcher(power);

            Assert.False(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task IsPausedAsync_ManualPauseWinsRegardlessOfPower()
        {
            // Manual pause must win even if auto-pause is off and we are on
            // AC — that's the whole point of the override.
            await WriteStateAsync(paused: true, autoPause: false);
            FakePowerSourceProvider power = new FakePowerSourceProvider { OnBattery = false };
            GlobalStateWatcher watcher = NewWatcher(power);

            Assert.True(await watcher.IsPausedAsync());
        }

        [Fact]
        public async Task GetPauseStateAsync_SurfacesBreakdown()
        {
            await WriteStateAsync(paused: false, autoPause: true);
            FakePowerSourceProvider power = new FakePowerSourceProvider { OnBattery = true };
            GlobalStateWatcher watcher = NewWatcher(power);

            PauseState state = await watcher.GetPauseStateAsync();

            Assert.False(state.ManuallyPaused);
            Assert.True(state.AutoPauseEnabled);
            Assert.True(state.OnBattery);
            Assert.True(state.EffectivePaused);
        }

        [Fact]
        public async Task GetPauseStateAsync_WithinRefreshInterval_DoesNotRepollPowerSource()
        {
            await WriteStateAsync(paused: false, autoPause: true);
            FakePowerSourceProvider power = new FakePowerSourceProvider { OnBattery = false };
            GlobalStateWatcher watcher = NewWatcher(power, TimeSpan.FromMinutes(5));

            await watcher.GetPauseStateAsync();
            await watcher.GetPauseStateAsync();
            await watcher.GetPauseStateAsync();

            // Three reads inside the cache window should only have hit the
            // power source provider once. Keeping IOKit calls off the hot
            // path is the whole point of the cache.
            Assert.Equal(1, power.CallCount);
        }
    }
}
