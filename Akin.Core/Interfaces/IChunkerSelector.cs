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
        /// Returns the chunker configuration to use when processing <paramref name="relativePath"/>.
        /// Always returns a configuration; unknown or extensionless files fall back to a
        /// generic plain-text format.
        /// </summary>
        ChunkerConfig SelectFor(string relativePath);

        /// <summary>
        /// A stable fingerprint over all configurations this selector can produce, used
        /// to detect when the chunking strategy has changed and the index must be rebuilt.
        /// </summary>
        string Fingerprint { get; }
    }
}
