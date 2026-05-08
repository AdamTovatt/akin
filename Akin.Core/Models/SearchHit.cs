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

        /// <summary>
        /// The number of additional regions in this file that were dropped because
        /// they exceeded <see cref="SearchOptions.MaxRegionsPerFile"/>. Zero when
        /// no truncation occurred.
        /// </summary>
        public int TruncatedRegionCount { get; init; }

        /// <summary>
        /// The total number of matched regions in this file before truncation —
        /// the count of regions that would have been returned if no per-file cap
        /// were applied. Equals <c>Regions.Count + TruncatedRegionCount</c>.
        /// </summary>
        public int TotalMatchCount => Regions.Count + TruncatedRegionCount;
    }
}
