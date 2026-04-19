namespace Akin.Core.Models
{
    /// <summary>
    /// A single matching region within a file returned by a search.
    /// </summary>
    public sealed record MatchedRegion
    {
        /// <summary>
        /// The first line of the region (1-based, inclusive).
        /// </summary>
        public required int StartLine { get; init; }

        /// <summary>
        /// The last line of the region (1-based, inclusive).
        /// </summary>
        public required int EndLine { get; init; }

        /// <summary>
        /// The cosine similarity score of this region against the query (0..1).
        /// </summary>
        public required float Score { get; init; }

        /// <summary>
        /// The chunk text, included only when the caller requested snippets.
        /// </summary>
        public string? Snippet { get; init; }
    }
}
