using System.Globalization;
using Akin.Core.Interfaces;
using Akin.Core.Models;
using Akin.Core.Services;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Default <see cref="ICommandFactory"/> backed by a <see cref="RepoContext"/>.
    /// Commands are constructed with the minimum set of services they need.
    /// Takes an optional <see cref="IProgress{T}"/> reporter that's threaded into
    /// commands whose work can report progress (search, reindex); the CLI wires
    /// up a console printer, the MCP path leaves it null.
    /// </summary>
    public sealed class CommandFactory : ICommandFactory
    {
        private readonly RepoContext _context;
        private readonly IProgress<IndexProgress>? _progress;

        public CommandFactory(RepoContext context, IProgress<IndexProgress>? progress = null)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _progress = progress;
        }

        public ICommand CreateFromArgs(string[] args)
        {
            if (args.Length == 0)
                return new HelpCommand();

            string verb = args[0].ToLowerInvariant();
            return verb switch
            {
                "search" => BuildSearchFromArgs(args),
                "status" => CreateStatus(),
                "reindex" => CreateReindex(),
                "help" or "--help" or "-h" => new HelpCommand(),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'. Run 'akin help' for usage."),
            };
        }

        public ICommand CreateSearch(string query, SearchOptions options)
        {
            return new SearchCommand(_context.SearchService, _context.EnsureIndexReadyAsync, query, options, _progress);
        }

        public ICommand CreateStatus()
        {
            return new StatusCommand(_context.Store, _context.RepoRoot, _context.IndexFolder, _context.IsIndexCompatible());
        }

        public ICommand CreateReindex()
        {
            return new ReindexCommand(_context.Indexer, _context.Store, _progress);
        }

        private SearchCommand BuildSearchFromArgs(string[] args)
        {
            if (args.Length < 2)
                throw new ArgumentException("Usage: akin search <query> [--max N] [--no-snippets] [--path GLOB] [--exclude GLOB] [--type KIND]");

            List<string> queryParts = new List<string>();
            int maxResults = 10;
            bool includeSnippets = true;
            List<string> includePaths = new List<string>();
            List<string> excludePaths = new List<string>();
            List<FileKind> includeKinds = new List<FileKind>();

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("--max", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--max requires a value.");
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxResults) || maxResults <= 0)
                        throw new ArgumentException("--max must be a positive integer.");
                }
                else if (arg.Equals("--no-snippets", StringComparison.OrdinalIgnoreCase))
                {
                    includeSnippets = false;
                }
                else if (arg.Equals("--path", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--path requires a glob pattern.");
                    includePaths.Add(args[++i]);
                }
                else if (arg.Equals("--exclude", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--exclude requires a glob pattern.");
                    excludePaths.Add(args[++i]);
                }
                else if (arg.Equals("--type", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--type requires a value (code, docs, or config).");
                    includeKinds.Add(ParseFileKind(args[++i]));
                }
                else
                {
                    queryParts.Add(arg);
                }
            }

            if (queryParts.Count == 0)
                throw new ArgumentException("Search requires a query string.");

            string query = string.Join(" ", queryParts);
            SearchOptions options = new SearchOptions
            {
                MaxResults = maxResults,
                IncludeSnippets = includeSnippets,
                IncludePaths = includePaths,
                ExcludePaths = excludePaths,
                IncludeKinds = includeKinds,
            };
            return new SearchCommand(_context.SearchService, _context.EnsureIndexReadyAsync, query, options, _progress);
        }

        private static FileKind ParseFileKind(string value)
        {
            if (!FileKinds.TryParse(value, out FileKind kind))
                throw new ArgumentException($"Unknown --type value '{value}'. Expected one of: {FileKinds.AcceptedValuesMessage}.");
            return kind;
        }
    }
}
