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
        /// The text shown to users in search results. For content chunks this
        /// is the raw slice of file content; for filename-only asset chunks
        /// it's a short human-readable placeholder like "[asset: path]".
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Optional override for what the indexer sends to the embedder. When
        /// null, the indexer composes path + Text so file path context
        /// contributes to semantic ranking. Filename-only asset chunks set
        /// this to just the path so the embedding focuses on the name and
        /// extension rather than the display wrapper.
        /// </summary>
        public string? EmbeddingText { get; init; }
    }
}
