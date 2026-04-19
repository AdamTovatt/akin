using Akin.Core.Models;

namespace Akin.Core.Interfaces
{
    /// <summary>
    /// A command that can be executed from either the CLI or the MCP layer.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The command result.</returns>
        Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken);
    }
}
