namespace Akin.Core.Models
{
    /// <summary>
    /// Lightweight file identity snapshot used to detect changes between runs without
    /// re-reading file contents. Size and modification time together catch virtually
    /// all real edits; if they match a stored fingerprint, we assume the file's
    /// existing chunks are still valid.
    /// </summary>
    public sealed record FileFingerprint
    {
        /// <summary>
        /// Repository-relative path, using forward slashes.
        /// </summary>
        public required string RelativePath { get; init; }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public required long Size { get; init; }

        /// <summary>
        /// Last-write time expressed as UTC ticks.
        /// </summary>
        public required long ModifiedTicks { get; init; }

        public static FileFingerprint FromFile(string relativePath, FileInfo info)
        {
            return new FileFingerprint
            {
                RelativePath = relativePath,
                Size = info.Length,
                ModifiedTicks = info.LastWriteTimeUtc.Ticks,
            };
        }

        public bool MatchesCurrentFile(FileInfo info)
        {
            return Size == info.Length && ModifiedTicks == info.LastWriteTimeUtc.Ticks;
        }
    }
}
