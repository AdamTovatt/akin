namespace Akin.Core.Models
{
    /// <summary>
    /// Progress update emitted by indexing and reconciliation operations so
    /// long-running work can report status to the CLI (or any other consumer).
    /// </summary>
    public sealed record IndexProgress
    {
        /// <summary>
        /// The phase of work currently in progress. "scanning" before the
        /// file list is known; "indexing" while files are being embedded;
        /// "persisting" during final write.
        /// </summary>
        public required string Phase { get; init; }

        /// <summary>
        /// The file being processed, when applicable.
        /// </summary>
        public string? CurrentFile { get; init; }

        /// <summary>
        /// Number of files processed so far.
        /// </summary>
        public int FilesDone { get; init; }

        /// <summary>
        /// Total number of files to process. Zero before scanning completes.
        /// </summary>
        public int FilesTotal { get; init; }

        /// <summary>
        /// Number of chunks added to the index so far.
        /// </summary>
        public int ChunksDone { get; init; }
    }
}
