namespace Akin.Core.Models
{
    /// <summary>
    /// A file-level search result, aggregating one or more matching chunks.
    /// </summary>
    public sealed record SearchHit
    {
        /// <summary>
        /// The file path relative to the repository root.
        /// </summary>
        public required string RelativePath { get; init; }

        /// <summary>
        /// The aggregate score for the file, defined as the maximum score across
        /// all matching regions. Used for ranking results.
        /// </summary>
        public required float AggregateScore { get; init; }

        /// <summary>
        /// All matching regions in this file, sorted by descending score.
        /// </summary>
        public required IReadOnlyList<MatchedRegion> Regions { get; init; }
    }
}
