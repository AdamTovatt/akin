namespace Akin.Core.Models
{
    /// <summary>
    /// Options controlling how a search is executed and how its results are shaped.
    /// </summary>
    public sealed record SearchOptions
    {
        /// <summary>
        /// The maximum number of files to return. Chunks are grouped by file after
        /// retrieval, so the underlying vector search pulls a larger candidate pool.
        /// </summary>
        public int MaxResults { get; init; } = 10;

        /// <summary>
        /// When true, each matching region carries the full chunk text. When false,
        /// regions only include path, line range, and score. Defaults to true.
        /// </summary>
        public bool IncludeSnippets { get; init; } = true;

        /// <summary>
        /// When set, regions below this cosine similarity threshold are excluded from
        /// the results entirely. When null, no threshold is applied.
        /// </summary>
        public float? MinimumScore { get; init; }
    }
}
