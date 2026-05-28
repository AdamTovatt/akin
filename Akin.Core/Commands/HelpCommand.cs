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
  akin search <query> [options]   Search the index
  akin status                     Show index status
  akin reindex                    Force a full reindex
  akin config                     Show current configuration
  akin config --set <key> <val>   Update a config setting
  akin pause                      Pause background indexing in all running
                                  MCP servers for this user (e.g. on battery)
  akin resume                     Resume background indexing
  akin --mcp                      Run as MCP server
  akin --version                  Show version
  akin help                       Show this help

Search options:
  --max N              Maximum number of files to return (default 10).
  --snippets           Include chunk text inline. Default is paths and line
                       ranges only (cheaper survey output).
  --max-per-file N     Cap matched regions returned per file (default 3).
                       Excess matches are summarized as "+N more matches".
  --path GLOB          Only consider files matching GLOB (repeatable). Supports
                       *, ?, and **. Example: --path "src/**" --path "**/*.cs".
  --exclude GLOB       Exclude files matching GLOB even if they match --path
                       (repeatable). Example: --exclude "**/*.test.*".
  --type KIND          Only consider files of the given kind (repeatable).
                       KIND is one of:
                         code    — source code (.cs, .ts, .py, .go, shell, ...)
                         docs    — prose (.md, .txt, README, LICENSE, ...)
                         config  — data/build/project files and tracked assets
                                   (.json, .yaml, .csproj, Dockerfile, images, ...)
                       Example: --type code --type config.

The index lives in .akin/ at the repository root. It is rebuilt automatically if
the embedding model or chunking strategy has changed since it was last built.
""";

        public Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new CommandResult(true, HelpText));
        }
    }
}
