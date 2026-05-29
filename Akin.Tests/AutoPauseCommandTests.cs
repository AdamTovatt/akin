using Akin.Core.Commands;
using Akin.Core.Models;

namespace Akin.Tests
{
    public class AutoPauseCommandTests : IDisposable
    {
        private readonly string _stateFolder;

        public AutoPauseCommandTests()
        {
            _stateFolder = Path.Combine(Path.GetTempPath(), "akin-autopause-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try { Directory.Delete(_stateFolder, recursive: true); }
            catch { }
        }

        private async Task<bool> ReadAutoPauseAsync() =>
            (await GlobalState.LoadAsync(_stateFolder)).AutoPauseOnBattery;

        [Fact]
        public async Task TurnOff_FromDefault_ClearsFlag()
        {
            CommandResult result = await new AutoPauseCommand(_stateFolder, enable: false).ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.False(await ReadAutoPauseAsync());
        }

        [Fact]
        public async Task TurnOn_AfterTurnOff_RestoresFlag()
        {
            await new AutoPauseCommand(_stateFolder, enable: false).ExecuteAsync(CancellationToken.None);

            CommandResult result = await new AutoPauseCommand(_stateFolder, enable: true).ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(await ReadAutoPauseAsync());
        }

        [Fact]
        public async Task TurnOn_WhenAlreadyOn_IsIdempotent()
        {
            // Defaults to on, so re-enabling without changing anything is the
            // common case we hit when the user runs `akin auto-pause on` to
            // double-check the current state.
            CommandResult result = await new AutoPauseCommand(_stateFolder, enable: true).ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("already on", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(await ReadAutoPauseAsync());
        }

        [Fact]
        public async Task TurnOff_WhenAlreadyOff_IsIdempotent()
        {
            await new AutoPauseCommand(_stateFolder, enable: false).ExecuteAsync(CancellationToken.None);

            CommandResult result = await new AutoPauseCommand(_stateFolder, enable: false).ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("already off", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(await ReadAutoPauseAsync());
        }

        [Fact]
        public async Task TurnOff_PreservesIndexingPausedFlag()
        {
            // Turning auto-pause off must not unpause an explicit manual pause.
            // The two flags are independent.
            await new GlobalState { IndexingPaused = true, AutoPauseOnBattery = true }.SaveAsync(_stateFolder);

            await new AutoPauseCommand(_stateFolder, enable: false).ExecuteAsync(CancellationToken.None);

            GlobalState state = await GlobalState.LoadAsync(_stateFolder);
            Assert.True(state.IndexingPaused);
            Assert.False(state.AutoPauseOnBattery);
        }
    }
}
