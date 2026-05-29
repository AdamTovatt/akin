using Akin.Core.Models;

namespace Akin.Tests
{
    public class GlobalStateTests : IDisposable
    {
        private readonly string _stateFolder;

        public GlobalStateTests()
        {
            _stateFolder = Path.Combine(Path.GetTempPath(), "akin-state-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try { Directory.Delete(_stateFolder, recursive: true); }
            catch { }
        }

        [Fact]
        public async Task LoadAsync_MissingFolder_ReturnsDefaultNotPaused()
        {
            GlobalState state = await GlobalState.LoadAsync(_stateFolder);

            Assert.False(state.IndexingPaused);
        }

        [Fact]
        public async Task LoadAsync_MissingFolder_ReturnsAutoPauseEnabledByDefault()
        {
            GlobalState state = await GlobalState.LoadAsync(_stateFolder);

            Assert.True(state.AutoPauseOnBattery);
        }

        [Fact]
        public async Task LoadAsync_LegacyStateJsonWithoutAutoPauseField_DefaultsAutoPauseOn()
        {
            // Pre-0.6 state.json files only contain indexingPaused. Loading
            // them must surface auto-pause as enabled so existing users opt
            // into the new default without editing their state file.
            Directory.CreateDirectory(_stateFolder);
            await File.WriteAllTextAsync(
                Path.Combine(_stateFolder, "state.json"),
                "{ \"indexingPaused\": false }");

            GlobalState state = await GlobalState.LoadAsync(_stateFolder);

            Assert.False(state.IndexingPaused);
            Assert.True(state.AutoPauseOnBattery);
        }

        [Fact]
        public async Task SaveThenLoad_RoundTripsPausedFlag()
        {
            await new GlobalState { IndexingPaused = true }.SaveAsync(_stateFolder);
            Assert.True((await GlobalState.LoadAsync(_stateFolder)).IndexingPaused);

            await new GlobalState { IndexingPaused = false }.SaveAsync(_stateFolder);
            Assert.False((await GlobalState.LoadAsync(_stateFolder)).IndexingPaused);
        }

        [Fact]
        public async Task SaveThenLoad_RoundTripsAutoPauseFlag()
        {
            await new GlobalState { AutoPauseOnBattery = false }.SaveAsync(_stateFolder);
            Assert.False((await GlobalState.LoadAsync(_stateFolder)).AutoPauseOnBattery);

            await new GlobalState { AutoPauseOnBattery = true }.SaveAsync(_stateFolder);
            Assert.True((await GlobalState.LoadAsync(_stateFolder)).AutoPauseOnBattery);
        }

        [Fact]
        public async Task SaveAsync_LeavesNoTempFileBehind()
        {
            await new GlobalState { IndexingPaused = true }.SaveAsync(_stateFolder);

            string[] leftovers = Directory.GetFiles(_stateFolder, "*.tmp");
            Assert.Empty(leftovers);
        }

        [Fact]
        public async Task LoadAsync_CorruptFile_FallsBackToDefault()
        {
            Directory.CreateDirectory(_stateFolder);
            await File.WriteAllTextAsync(Path.Combine(_stateFolder, "state.json"), "{ this is not json");

            GlobalState state = await GlobalState.LoadAsync(_stateFolder);

            Assert.False(state.IndexingPaused);
        }
    }
}
