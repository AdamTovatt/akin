using System.Text;
using Akin.Core.Interfaces;
using Akin.Core.Models;
using Akin.Core.Services;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Embeds the query, retrieves top hits from the index, and formats them
    /// for text output (CLI or MCP). Takes a delegate for "ensure index is ready"
    /// rather than the whole <see cref="RepoContext"/> so the dependency surface
    /// stays minimal.
    /// </summary>
    public sealed class SearchCommand : ICommand
    {
        private readonly ISearchService _searchService;
        private readonly Func<IProgress<IndexProgress>?, CancellationToken, Task> _ensureReady;
        private readonly string _query;
        private readonly SearchOptions _options;
        private readonly IProgress<IndexProgress>? _progress;

        public SearchCommand(
            ISearchService searchService,
            Func<IProgress<IndexProgress>?, CancellationToken, Task> ensureReady,
            string query,
            SearchOptions options,
            IProgress<IndexProgress>? progress = null)
        {
            ArgumentNullException.ThrowIfNull(searchService);
            ArgumentNullException.ThrowIfNull(ensureReady);
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
            ArgumentNullException.ThrowIfNull(options);

            _searchService = searchService;
            _ensureReady = ensureReady;
            _query = query;
            _options = options;
            _progress = progress;
        }

        public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            await _ensureReady(_progress, cancellationToken);

            IReadOnlyList<SearchHit> hits = await _searchService.SearchAsync(_query, _options, cancellationToken);

            if (hits.Count == 0)
            {
                return new CommandResult(true, "No matches.");
            }

            StringBuilder details = new StringBuilder();
            foreach (SearchHit hit in hits)
            {
                details.Append(hit.RelativePath)
                       .Append("  (score ")
                       .Append(hit.AggregateScore.ToString("0.000"))
                       .Append(", ")
                       .Append(hit.Regions.Count)
                       .Append(' ')
                       .Append(Pluralize.Of(hit.Regions.Count, "match", "matches"))
                       .AppendLine(")");

                foreach (MatchedRegion region in hit.Regions)
                {
                    details.Append("  lines ")
                           .Append(region.StartLine)
                           .Append('-')
                           .Append(region.EndLine)
                           .Append("  score ")
                           .AppendLine(region.Score.ToString("0.000"));

                    if (region.Snippet != null)
                    {
                        foreach (string line in region.Snippet.Split('\n'))
                        {
                            details.Append("    ").AppendLine(line.TrimEnd('\r'));
                        }
                    }
                }
                details.AppendLine();
            }

            string message = $"{hits.Count} {Pluralize.Of(hits.Count, "hit")}.";
            return new CommandResult(true, message, details.ToString().TrimEnd());
        }
    }
}
