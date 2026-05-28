using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Globally pauses background indexing for every <c>akin</c> MCP server
    /// running for the current user. The change is persisted to
    /// <c>~/.akin/state.json</c>; running servers notice within a few seconds
    /// of their next pause-state poll. Search continues to work from whatever
    /// is already on disk.
    /// </summary>
    public sealed class PauseCommand : ICommand
    {
        private readonly string _stateFolder;

        public PauseCommand(string stateFolder)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateFolder);
            _stateFolder = stateFolder;
        }

        public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            GlobalState current = await GlobalState.LoadAsync(_stateFolder, cancellationToken);

            if (current.IndexingPaused)
                return new CommandResult(true, "Indexing already paused.");

            GlobalState updated = current with { IndexingPaused = true };
            await updated.SaveAsync(_stateFolder, cancellationToken);
            return new CommandResult(true, "Indexing paused. Running MCP servers will stop indexing within a few seconds. Run 'akin resume' to re-enable.");
        }
    }
}
