namespace Akin.Core.Models
{
    /// <summary>
    /// A chunker configuration selected for a specific file, built from
    /// VectorSharp.Chunking predefined formats or a custom set.
    /// </summary>
    public sealed record ChunkerConfig
    {
        /// <summary>
        /// A short name identifying the format (e.g. "csharp", "markdown", "plaintext").
        /// Used in the manifest fingerprint and for logging.
        /// </summary>
        public required string FormatName { get; init; }

        /// <summary>
        /// The break strings to pass to <c>VectorSharp.Chunking.ChunkReaderOptions</c>.
        /// </summary>
        public required IReadOnlyList<string> BreakStrings { get; init; }

        /// <summary>
        /// The stop signals to pass to <c>VectorSharp.Chunking.ChunkReaderOptions</c>.
        /// </summary>
        public required IReadOnlyList<string> StopSignals { get; init; }

        /// <summary>
        /// Maximum tokens per chunk. Enforced via the token counter passed to the chunker.
        /// </summary>
        public required int MaxTokensPerChunk { get; init; }
    }
}
