using Akin.Core.Models;

namespace Akin.Core.Interfaces
{
    /// <summary>
    /// Resolves a <see cref="ChunkerConfig"/> for a given file path. The selection
    /// is based on the file extension, with a plain-text fallback for unknown types.
    /// </summary>
    public interface IChunkerSelector
    {
        /// <summary>
        /// Returns the chunker configuration to use when processing <paramref name="relativePath"/>,
        /// or null when the file is not a chunkable text file. A null return does not
        /// necessarily mean the file is skipped outright — see <see cref="ShouldIndexByFilename"/>.
        /// </summary>
        ChunkerConfig? SelectFor(string relativePath);

        /// <summary>
        /// Returns true for files that can't be meaningfully chunked but whose
        /// filename should still be indexed — images, PDFs, design documents.
        /// The caller emits a single synthetic chunk for these so queries like
        /// "app icon" or "logo" can surface the file by name.
        /// </summary>
        bool ShouldIndexByFilename(string relativePath);

        /// <summary>
        /// A stable fingerprint over all configurations this selector can produce, used
        /// to detect when the chunking strategy has changed and the index must be rebuilt.
        /// </summary>
        string Fingerprint { get; }
    }
}
