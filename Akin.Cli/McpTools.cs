using System.ComponentModel;
using Akin.Core.Interfaces;
using Akin.Core.Models;
using ModelContextProtocol.Server;

namespace Akin.Cli
{
    /// <summary>
    /// MCP tools exposed by the Akin server. Each tool builds the same
    /// <see cref="ICommand"/> the CLI would construct and executes it, so output
    /// formatting and behavior stay consistent between surfaces.
    /// </summary>
    [McpServerToolType]
    public sealed class McpTools
    {
        private readonly ICommandFactory _factory;

        public McpTools(ICommandFactory factory)
        {
            ArgumentNullException.ThrowIfNull(factory);
            _factory = factory;
        }

        [McpServerTool(Name = "akin_search")]
        [Description("Semantic code search over the current repository. Returns ranked files with line ranges and optional code snippets. Use this when looking for code by meaning or behavior rather than exact text. For broad surveys, pass includeSnippets=false to get file names and line ranges only; for focused follow-ups, leave includeSnippets=true to get the chunk text inline.")]
        public async Task<string> SearchAsync(
            [Description("The natural-language or keyword query describing what to find.")]
            string query,
            [Description("Maximum number of files to return. Default 10.")]
            int maxResults = 10,
            [Description("When true, each matching region includes the chunk text. When false, only paths, line ranges, and scores are returned. Default true.")]
            bool includeSnippets = true,
            [Description("Optional minimum cosine similarity score (0..1). Regions below this are dropped. Leave unset for no threshold.")]
            float? minimumScore = null,
            CancellationToken cancellationToken = default)
        {
            SearchOptions options = new SearchOptions
            {
                MaxResults = maxResults,
                IncludeSnippets = includeSnippets,
                MinimumScore = minimumScore,
            };

            ICommand command = _factory.CreateSearch(query, options);
            CommandResult result = await command.ExecuteAsync(cancellationToken);
            return FormatResult(result);
        }

        [McpServerTool(Name = "akin_status")]
        [Description("Reports the current state of the Akin index for this repository: whether it is ready, how many files and chunks are stored, and whether it is compatible with the current embedding/chunker configuration.")]
        public async Task<string> StatusAsync(CancellationToken cancellationToken = default)
        {
            ICommand command = _factory.CreateStatus();
            CommandResult result = await command.ExecuteAsync(cancellationToken);
            return FormatResult(result);
        }

        [McpServerTool(Name = "akin_reindex")]
        [Description("Forces a full rebuild of the Akin index for this repository. Use this when the index appears stale or incomplete. Reindexing a small-to-medium repository typically takes a few seconds.")]
        public async Task<string> ReindexAsync(CancellationToken cancellationToken = default)
        {
            ICommand command = _factory.CreateReindex();
            CommandResult result = await command.ExecuteAsync(cancellationToken);
            return FormatResult(result);
        }

        private static string FormatResult(CommandResult result)
        {
            if (string.IsNullOrEmpty(result.Details))
                return result.Message;
            return $"{result.Message}\n\n{result.Details}";
        }
    }
}
