namespace Akin.Core.Models
{
    /// <summary>
    /// A chunk produced by the indexer before it is handed to the index store.
    /// The store assigns the identifier when the draft is accepted, which keeps
    /// id allocation strictly serialised inside the store and removes any race
    /// with callers that might be reserving ids concurrently.
    /// </summary>
    public sealed record ChunkDraft
    {
        /// <summary>
        /// The file path relative to the repository root, using forward slashes.
        /// </summary>
        public required string RelativePath { get; init; }

        /// <summary>
        /// The first line of this chunk in the source file (1-based, inclusive).
        /// </summary>
        public required int StartLine { get; init; }

        /// <summary>
        /// The last line of this chunk in the source file (1-based, inclusive).
        /// </summary>
        public required int EndLine { get; init; }

        /// <summary>
        /// The raw chunk text.
        /// </summary>
        public required string Text { get; init; }
    }
}
