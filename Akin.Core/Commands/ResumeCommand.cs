using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Globally resumes background indexing previously paused via
    /// <see cref="PauseCommand"/>. Running MCP servers pick up the change on
    /// their next pause-state poll; queued file events flush within the next
    /// flush tick (~2s) and any missed work is caught up by the next
    /// reconciliation tick (up to 5 minutes).
    /// </summary>
    public sealed class ResumeCommand : ICommand
    {
        public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            GlobalState current = await GlobalState.LoadAsync(cancellationToken);

            if (!current.IndexingPaused)
                return new CommandResult(true, "Indexing already active.");

            GlobalState updated = current with { IndexingPaused = false };
            await updated.SaveAsync(cancellationToken);
            return new CommandResult(true, "Indexing resumed. Running MCP servers will catch up on any missed changes.");
        }
    }
}
