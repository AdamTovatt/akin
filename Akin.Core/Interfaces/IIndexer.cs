using Akin.Core.Models;

namespace Akin.Core.Interfaces
{
    /// <summary>
    /// Orchestrates scanning, chunking, and embedding to populate an <see cref="IIndexStore"/>.
    /// </summary>
    public interface IIndexer
    {
        /// <summary>
        /// Performs a full rebuild of the index: scans all tracked files, chunks them,
        /// embeds every chunk, and replaces the contents of the index store. Emits
        /// progress updates via <paramref name="progress"/> if supplied.
        /// </summary>
        Task ReindexAllAsync(IProgress<IndexProgress>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rebuilds the index entries for a single file. Removes all existing chunks for
        /// the file, re-chunks and re-embeds the current content, and writes the result.
        /// </summary>
        Task ReindexFileAsync(string relativePath, CancellationToken cancellationToken = default);
    }
}
