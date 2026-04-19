using Akin.Core.Models;

namespace Akin.Core.Interfaces
{
    /// <summary>
    /// Builds <see cref="ICommand"/> instances for both the CLI (from an argv array)
    /// and the MCP server (from typed parameters). A single factory feeds both
    /// surfaces so argument interpretation and command construction stay
    /// consistent across them.
    /// </summary>
    public interface ICommandFactory
    {
        /// <summary>
        /// Resolves a CLI argv array to a concrete command. Throws <see cref="ArgumentException"/>
        /// with a user-facing message when args are malformed.
        /// </summary>
        ICommand CreateFromArgs(string[] args);

        /// <summary>
        /// Builds a search command for the MCP path, where arguments arrive
        /// typed instead of as strings.
        /// </summary>
        ICommand CreateSearch(string query, SearchOptions options);

        /// <summary>
        /// Builds a status command.
        /// </summary>
        ICommand CreateStatus();

        /// <summary>
        /// Builds a force-full-reindex command.
        /// </summary>
        ICommand CreateReindex();
    }
}
