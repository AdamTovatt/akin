using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Prints Akin's CLI usage to stdout. Takes no dependencies; safe to run
    /// without a repository context.
    /// </summary>
    public sealed class HelpCommand : ICommand
    {
        private const string HelpText = """
Akin — semantic code search for AI agents and CLI users.

Usage:
  akin search <query> [--max N] [--no-snippets] [--min-score S]   Search the index
  akin status                                                     Show index status
  akin reindex                                                    Force a full reindex
  akin --mcp                                                      Run as MCP server
  akin --version                                                  Show version
  akin help                                                       Show this help

The index lives in .akin/ at the repository root. It is rebuilt automatically if
the embedding model or chunking strategy has changed since it was last built.
""";

        public Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new CommandResult(true, HelpText));
        }
    }
}
