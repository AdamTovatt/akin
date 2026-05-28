using Akin.Core.Commands;
using Akin.Core.Models;

namespace Akin.Tests
{
    public class PauseResumeCommandTests : IDisposable
    {
        private readonly string _stateFolder;

        public PauseResumeCommandTests()
        {
            _stateFolder = Path.Combine(Path.GetTempPath(), "akin-cmd-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try { Directory.Delete(_stateFolder, recursive: true); }
            catch { }
        }

        private async Task<bool> IsPausedAsync() =>
            (await GlobalState.LoadAsync(_stateFolder)).IndexingPaused;

        [Fact]
        public async Task PauseCommand_FromActive_SetsPausedFlag()
        {
            CommandResult result = await new PauseCommand(_stateFolder).ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(await IsPausedAsync());
        }

        [Fact]
        public async Task PauseCommand_WhenAlreadyPaused_IsIdempotent()
        {
            await new PauseCommand(_stateFolder).ExecuteAsync(CancellationToken.None);

            CommandResult result = await new PauseCommand(_stateFolder).ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("already paused", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(await IsPausedAsync());
        }

        [Fact]
        public async Task ResumeCommand_FromPaused_ClearsPausedFlag()
        {
            await new PauseCommand(_stateFolder).ExecuteAsync(CancellationToken.None);

            CommandResult result = await new ResumeCommand(_stateFolder).ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.False(await IsPausedAsync());
        }

        [Fact]
        public async Task ResumeCommand_WhenAlreadyActive_IsIdempotent()
        {
            CommandResult result = await new ResumeCommand(_stateFolder).ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("already active", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(await IsPausedAsync());
        }
    }
}
