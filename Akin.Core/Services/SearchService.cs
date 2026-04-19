using Akin.Core.Interfaces;
using Akin.Core.Models;
using VectorSharp.Embedding;

namespace Akin.Core.Services
{
    /// <summary>
    /// Embeds a query, pulls candidates from the index store, groups them by file, and
    /// ranks files by their best-matching chunk.
    /// </summary>
    public sealed class SearchService : ISearchService
    {
        private readonly EmbeddingService _embedder;
        private readonly IIndexStore _store;

        public SearchService(EmbeddingService embedder, IIndexStore store)
        {
            ArgumentNullException.ThrowIfNull(embedder);
            ArgumentNullException.ThrowIfNull(store);
            _embedder = embedder;
            _store = store;
        }

        public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
            ArgumentNullException.ThrowIfNull(options);

            if (!_store.IsReady || _store.ChunkCount == 0)
                return Array.Empty<SearchHit>();

            float[] queryVector = await _embedder.EmbedAsync(query, EmbeddingPurpose.Query, cancellationToken);

            int candidatePoolSize = Math.Max(options.MaxResults * 5, 25);
            IReadOnlyList<(int Id, float Score)> candidates = await _store.FindMostSimilarAsync(queryVector, candidatePoolSize, cancellationToken);

            Dictionary<string, List<MatchedRegion>> byFile = new Dictionary<string, List<MatchedRegion>>(StringComparer.Ordinal);
            foreach ((int id, float score) in candidates)
            {
                if (options.MinimumScore is float min && score < min)
                    continue;

                ChunkInfo? chunk = _store.GetChunk(id);
                if (chunk == null)
                    continue;

                if (!byFile.TryGetValue(chunk.RelativePath, out List<MatchedRegion>? regions))
                {
                    regions = new List<MatchedRegion>();
                    byFile[chunk.RelativePath] = regions;
                }

                regions.Add(new MatchedRegion
                {
                    StartLine = chunk.StartLine,
                    EndLine = chunk.EndLine,
                    Score = score,
                    Snippet = options.IncludeSnippets ? chunk.Text : null,
                });
            }

            List<SearchHit> hits = new List<SearchHit>(byFile.Count);
            foreach (KeyValuePair<string, List<MatchedRegion>> pair in byFile)
            {
                List<MatchedRegion> merged = MergeRegions(pair.Value);
                merged.Sort((a, b) => b.Score.CompareTo(a.Score));

                hits.Add(new SearchHit
                {
                    RelativePath = pair.Key,
                    AggregateScore = merged[0].Score,
                    Regions = merged,
                });
            }

            hits.Sort((a, b) => b.AggregateScore.CompareTo(a.AggregateScore));

            if (hits.Count > options.MaxResults)
            {
                hits = hits.Take(options.MaxResults).ToList();
            }

            return hits;
        }

        /// <summary>
        /// Merges regions from the same file that are contiguous or overlapping into
        /// a single region. The merged region keeps the earlier start, the later end,
        /// and the higher score. Snippets from contiguous chunks are joined with a
        /// newline separator so line breaks are never lost; snippets from regions
        /// that overlap keep the earlier region's text (the later one is a duplicate
        /// view of the same lines).
        /// </summary>
        private static List<MatchedRegion> MergeRegions(List<MatchedRegion> regions)
        {
            if (regions.Count <= 1)
                return new List<MatchedRegion>(regions);

            List<MatchedRegion> sortedByStart = regions.OrderBy(r => r.StartLine).ToList();
            List<MatchedRegion> merged = new List<MatchedRegion>();

            MatchedRegion current = sortedByStart[0];
            for (int i = 1; i < sortedByStart.Count; i++)
            {
                MatchedRegion next = sortedByStart[i];
                bool contiguous = next.StartLine <= current.EndLine + 1;
                if (contiguous)
                {
                    int mergedEnd = Math.Max(current.EndLine, next.EndLine);
                    float mergedScore = Math.Max(current.Score, next.Score);
                    string? mergedSnippet = current.Snippet;
                    bool nonOverlapping = current.EndLine < next.StartLine;

                    if (mergedSnippet != null && next.Snippet != null && nonOverlapping)
                    {
                        string separator = mergedSnippet.EndsWith('\n') ? string.Empty : "\n";
                        mergedSnippet = mergedSnippet + separator + next.Snippet;
                    }
                    else if (mergedSnippet == null)
                    {
                        mergedSnippet = next.Snippet;
                    }

                    current = new MatchedRegion
                    {
                        StartLine = current.StartLine,
                        EndLine = mergedEnd,
                        Score = mergedScore,
                        Snippet = mergedSnippet,
                    };
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }
            merged.Add(current);

            return merged;
        }
    }
}
