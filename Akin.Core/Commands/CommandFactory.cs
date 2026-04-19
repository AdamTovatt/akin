using System.Globalization;
using Akin.Core.Interfaces;
using Akin.Core.Models;
using Akin.Core.Services;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Default <see cref="ICommandFactory"/> backed by a <see cref="RepoContext"/>.
    /// Commands are constructed with the minimum set of services they need.
    /// </summary>
    public sealed class CommandFactory : ICommandFactory
    {
        private readonly RepoContext _context;

        public CommandFactory(RepoContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
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
            return new SearchCommand(_context.SearchService, _context.EnsureIndexReadyAsync, query, options);
        }

        public ICommand CreateStatus()
        {
            return new StatusCommand(_context.Store, _context.RepoRoot, _context.IndexFolder, _context.IsIndexCompatible());
        }

        public ICommand CreateReindex()
        {
            return new ReindexCommand(_context.Indexer, _context.Store);
        }

        private SearchCommand BuildSearchFromArgs(string[] args)
        {
            if (args.Length < 2)
                throw new ArgumentException("Usage: akin search <query> [--max N] [--no-snippets] [--min-score S]");

            List<string> queryParts = new List<string>();
            int maxResults = 10;
            bool includeSnippets = true;
            float? minScore = null;

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
                else if (arg.Equals("--min-score", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--min-score requires a value.");
                    if (!float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                        throw new ArgumentException("--min-score must be a number.");
                    minScore = parsed;
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
                MinimumScore = minScore,
            };
            return new SearchCommand(_context.SearchService, _context.EnsureIndexReadyAsync, query, options);
        }
    }
}
