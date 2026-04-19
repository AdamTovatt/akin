using Akin.Core.Models;

namespace Akin.Core.Interfaces
{
    /// <summary>
    /// The sole owner of the on-disk layout for a single repository's index
    /// (vectors, chunk metadata, fingerprints, and manifest). Wraps the vector
    /// backend behind one interface so a future out-of-process (daemon)
    /// implementation can plug in without rewriting callers.
    ///
    /// All identifier allocation is internal to the implementation: callers
    /// submit <see cref="ChunkDraft"/>s and receive a <see cref="ChunkInfo"/>
    /// with the assigned id. This keeps id assignment atomic with the write
    /// and removes any possibility of concurrent callers producing duplicate
    /// ids.
    /// </summary>
    public interface IIndexStore : IAsyncDisposable
    {
        /// <summary>
        /// The manifest for the currently loaded index, or null if no index exists.
        /// </summary>
        Manifest? Manifest { get; }

        /// <summary>
        /// Returns true when an index has been loaded or created and is ready for search.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// The current number of chunks in the index.
        /// </summary>
        int ChunkCount { get; }

        /// <summary>
        /// The current number of distinct files represented in the index.
        /// </summary>
        int FileCount { get; }

        /// <summary>
        /// Loads an existing index from disk. If the on-disk state is missing or
        /// inconsistent, the store remains empty with <see cref="IsReady"/> false.
        /// </summary>
        Task OpenAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces the full index atomically with the provided chunks, fingerprints,
        /// and manifest. The store assigns sequential ids to each chunk as it is
        /// inserted.
        /// </summary>
        Task ReplaceAllAsync(
            Manifest manifest,
            IReadOnlyList<(ChunkDraft Draft, float[] Embedding)> chunks,
            IReadOnlyList<FileFingerprint> fingerprints,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces all chunks and fingerprint for a single file. Existing chunks for
        /// <paramref name="relativePath"/> are removed first, then the new ones are
        /// added. Pass a null <paramref name="fingerprint"/> to clear any stored
        /// fingerprint for the file (e.g. when the file becomes untrackable).
        /// </summary>
        Task ReplaceFileAsync(
            string relativePath,
            IReadOnlyList<(ChunkDraft Draft, float[] Embedding)> chunks,
            FileFingerprint? fingerprint,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes all chunks and the fingerprint belonging to the given file.
        /// Returns the number of chunks removed.
        /// </summary>
        Task<int> RemoveFileAsync(string relativePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a similarity search and returns the top <paramref name="count"/>
        /// chunk ids along with their similarity scores.
        /// </summary>
        Task<IReadOnlyList<(int Id, float Score)>> FindMostSimilarAsync(
            float[] queryVector,
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves chunk metadata by id, or null if no such chunk exists.
        /// </summary>
        ChunkInfo? GetChunk(int id);

        /// <summary>
        /// Enumerates all chunks currently in the index.
        /// </summary>
        IReadOnlyCollection<ChunkInfo> AllChunks();

        /// <summary>
        /// Retrieves the stored fingerprint for a file, or null if none is recorded.
        /// </summary>
        FileFingerprint? GetFingerprint(string relativePath);

        /// <summary>
        /// Enumerates all stored file fingerprints.
        /// </summary>
        IReadOnlyCollection<FileFingerprint> AllFingerprints();

        /// <summary>
        /// Builds a snapshot of the current index state.
        /// </summary>
        IndexStatus GetStatus();

        /// <summary>
        /// Opens a batch scope during which individual write operations skip their
        /// normal persist-to-disk step. A single persist runs when the scope is
        /// disposed. Intended for bulk callers like the reconciler that apply many
        /// writes in sequence. Nested scopes are supported; only the outermost
        /// disposal triggers the persist.
        ///
        /// Behaviour during a batch: in-memory state updates eagerly, so searches
        /// running alongside the batch observe the new data immediately. Only the
        /// on-disk commit is deferred. If the process crashes before the scope
        /// disposes, disk state reverts to the last successful persist and the
        /// in-memory updates are lost.
        /// </summary>
        ValueTask<IAsyncDisposable> BeginBatchAsync(CancellationToken cancellationToken = default);
    }
}
