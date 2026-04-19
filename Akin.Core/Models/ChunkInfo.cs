namespace Akin.Core.Models
{
    /// <summary>
    /// Metadata for a single chunk in the index. Paired with a vector of the same
    /// <see cref="Id"/> in the vector store.
    /// </summary>
    public sealed record ChunkInfo
    {
        /// <summary>
        /// The unique identifier for this chunk within the index. Assigned by the
        /// index store when the chunk is added.
        /// </summary>
        public required int Id { get; init; }

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
