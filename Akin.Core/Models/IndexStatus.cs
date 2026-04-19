namespace Akin.Core.Models
{
    /// <summary>
    /// Snapshot of the current state of an index.
    /// </summary>
    public sealed record IndexStatus
    {
        /// <summary>
        /// The number of distinct files represented in the index.
        /// </summary>
        public required int FileCount { get; init; }

        /// <summary>
        /// The total number of chunks in the index.
        /// </summary>
        public required int ChunkCount { get; init; }

        /// <summary>
        /// The manifest of the current index, or null if the index has not been
        /// built yet.
        /// </summary>
        public required Manifest? Manifest { get; init; }

        /// <summary>
        /// True when the index has been built at least once and is queryable.
        /// </summary>
        public required bool IsReady { get; init; }
    }
}
