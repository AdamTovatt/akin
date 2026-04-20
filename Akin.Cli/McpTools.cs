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
        [Description("Semantic code search over the current repository. Returns ranked files with line ranges and optional code snippets. Use this when looking for code by meaning or behavior rather than exact text. For broad surveys, pass includeSnippets=false to get file names and line ranges only; for focused follow-ups, leave includeSnippets=true to get the chunk text inline. Scope the search with paths/excludePaths (glob patterns) and/or includeTypes (file categories: \"code\", \"docs\", \"config\").")]
        public async Task<string> SearchAsync(
            [Description("The natural-language or keyword query describing what to find.")]
            string query,
            [Description("Maximum number of files to return. Default 10.")]
            int maxResults = 10,
            [Description("When true, each matching region includes the chunk text. When false, only paths and line ranges are returned. Default true.")]
            bool includeSnippets = true,
            [Description("Optional glob patterns (relative to repo root) restricting search to matching files. Supports *, ?, and **. Example: [\"src/**\", \"**/*.cs\"].")]
            string[]? paths = null,
            [Description("Optional glob patterns (relative to repo root) excluding matching files even if they also match a paths pattern. Example: [\"**/*.test.*\", \"**/node_modules/**\"].")]
            string[]? excludePaths = null,
            [Description("Optional list of file categories to include. Valid values: \"code\" (source code), \"docs\" (markdown, README, LICENSE, plain-text docs), \"config\" (JSON/YAML/TOML, project/build files, assets). When omitted, all categories are considered.")]
            string[]? includeTypes = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryParseKinds(includeTypes, out FileKind[] kinds, out string? parseError))
                return parseError!;

            SearchOptions options = new SearchOptions
            {
                MaxResults = maxResults,
                IncludeSnippets = includeSnippets,
                IncludePaths = paths ?? Array.Empty<string>(),
                ExcludePaths = excludePaths ?? Array.Empty<string>(),
                IncludeKinds = kinds,
            };

            ICommand command = _factory.CreateSearch(query, options);
            CommandResult result = await command.ExecuteAsync(cancellationToken);
            return FormatResult(result);
        }

        private static bool TryParseKinds(string[]? values, out FileKind[] parsed, out string? error)
        {
            if (values == null || values.Length == 0)
            {
                parsed = Array.Empty<FileKind>();
                error = null;
                return true;
            }

            FileKind[] result = new FileKind[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (!FileKinds.TryParse(values[i], out result[i]))
                {
                    parsed = Array.Empty<FileKind>();
                    error = $"Unknown includeTypes value '{values[i]}'. Expected one of: {FileKinds.AcceptedValuesMessage}.";
                    return false;
                }
            }
            parsed = result;
            error = null;
            return true;
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
