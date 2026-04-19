using Akin.Core.Interfaces;
using Akin.Core.Models;
using VectorSharp.Embedding;

namespace Akin.Core.Services
{
    /// <summary>
    /// Orchestrates indexing: ask the scanner for tracked files, let the
    /// <see cref="FileChunker"/> turn each one into chunk drafts, embed the drafts,
    /// and hand everything to the <see cref="IIndexStore"/>. Neither file I/O nor
    /// chunking lives here — this class is scheduling and coordination only.
    /// </summary>
    public sealed class Indexer : IIndexer
    {
        private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(15);

        private readonly IRepoScanner _scanner;
        private readonly FileChunker _fileChunker;
        private readonly IIndexStore _store;
        private readonly EmbeddingService _embedder;
        private readonly IChunkerSelector _chunkerSelector;
        private readonly string _embeddingModelId;
        private readonly TimeSpan _flushInterval;

        public Indexer(
            IRepoScanner scanner,
            FileChunker fileChunker,
            IIndexStore store,
            EmbeddingService embedder,
            IChunkerSelector chunkerSelector,
            string embeddingModelId,
            TimeSpan? flushInterval = null)
        {
            ArgumentNullException.ThrowIfNull(scanner);
            ArgumentNullException.ThrowIfNull(fileChunker);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(embedder);
            ArgumentNullException.ThrowIfNull(chunkerSelector);
            ArgumentException.ThrowIfNullOrWhiteSpace(embeddingModelId);

            _scanner = scanner;
            _fileChunker = fileChunker;
            _store = store;
            _embedder = embedder;
            _chunkerSelector = chunkerSelector;
            _embeddingModelId = embeddingModelId;
            _flushInterval = flushInterval ?? DefaultFlushInterval;
        }

        public async Task ReindexAllAsync(IProgress<IndexProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new IndexProgress { Phase = "scanning" });

            IReadOnlyList<string> files = await _scanner.ScanAsync(cancellationToken);

            Manifest manifest = new Manifest
            {
                SchemaVersion = Manifest.CurrentSchemaVersion,
                EmbeddingModel = _embeddingModelId,
                EmbeddingDimension = _embedder.Dimension,
                ChunkerFingerprint = _chunkerSelector.Fingerprint,
                LastRebuiltUtc = DateTime.UtcNow,
            };

            // Open a batch so each file's write doesn't persist immediately; we
            // checkpoint to disk on a time interval instead. The outer batch also
            // ensures the final dispose (in the finally block) commits whatever
            // remains, so the happy path and the time-based flush share the same
            // code path.
            IAsyncDisposable batch = await _store.BeginBatchAsync(cancellationToken);
            try
            {
                // Reset in-memory state up front and stamp the new manifest. The
                // empty fingerprint set is important: on a mid-reindex crash, the
                // next startup's reconciler sees which files still need work.
                await _store.ReplaceAllAsync(manifest, Array.Empty<(ChunkDraft, float[])>(), Array.Empty<FileFingerprint>(), cancellationToken);

                progress?.Report(new IndexProgress
                {
                    Phase = "indexing",
                    FilesDone = 0,
                    FilesTotal = files.Count,
                });

                DateTime lastFlush = DateTime.UtcNow;
                int chunksDone = 0;

                for (int i = 0; i < files.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string relativePath = files[i];

                    progress?.Report(new IndexProgress
                    {
                        Phase = "indexing",
                        CurrentFile = relativePath,
                        FilesDone = i,
                        FilesTotal = files.Count,
                        ChunksDone = chunksDone,
                    });

                    PreparedFile? prepared = await _fileChunker.PrepareAsync(relativePath, cancellationToken);
                    if (prepared == null) continue;

                    if (prepared.Chunks.Count == 0)
                    {
                        await _store.ReplaceFileAsync(relativePath, Array.Empty<(ChunkDraft, float[])>(), prepared.Fingerprint, cancellationToken);
                    }
                    else
                    {
                        IReadOnlyList<string> texts = prepared.Chunks.Select(c => c.Text).ToList();
                        float[][] embeddings = await _embedder.EmbedBatchAsync(texts, EmbeddingPurpose.Document, cancellationToken);

                        List<(ChunkDraft, float[])> paired = new List<(ChunkDraft, float[])>(prepared.Chunks.Count);
                        for (int k = 0; k < prepared.Chunks.Count; k++)
                        {
                            paired.Add((prepared.Chunks[k], embeddings[k]));
                        }

                        await _store.ReplaceFileAsync(relativePath, paired, prepared.Fingerprint, cancellationToken);
                        chunksDone += prepared.Chunks.Count;
                    }

                    if (DateTime.UtcNow - lastFlush >= _flushInterval)
                    {
                        progress?.Report(new IndexProgress
                        {
                            Phase = "persisting",
                            FilesDone = i + 1,
                            FilesTotal = files.Count,
                            ChunksDone = chunksDone,
                        });

                        // Disposing triggers a persist; the new scope resumes deferral.
                        await batch.DisposeAsync();
                        batch = await _store.BeginBatchAsync(cancellationToken);
                        lastFlush = DateTime.UtcNow;
                    }
                }

                progress?.Report(new IndexProgress
                {
                    Phase = "persisting",
                    FilesDone = files.Count,
                    FilesTotal = files.Count,
                    ChunksDone = chunksDone,
                });
            }
            finally
            {
                await batch.DisposeAsync();
            }
        }

        public async Task ReindexFileAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

            PreparedFile? prepared = await _fileChunker.PrepareAsync(relativePath, cancellationToken);
            if (prepared == null)
            {
                await _store.RemoveFileAsync(relativePath, cancellationToken);
                return;
            }

            if (prepared.Chunks.Count == 0)
            {
                await _store.ReplaceFileAsync(relativePath, Array.Empty<(ChunkDraft, float[])>(), prepared.Fingerprint, cancellationToken);
                return;
            }

            IReadOnlyList<string> texts = prepared.Chunks.Select(c => c.Text).ToList();
            float[][] embeddings = await _embedder.EmbedBatchAsync(texts, EmbeddingPurpose.Document, cancellationToken);

            List<(ChunkDraft, float[])> paired = new List<(ChunkDraft, float[])>(prepared.Chunks.Count);
            for (int i = 0; i < prepared.Chunks.Count; i++)
            {
                paired.Add((prepared.Chunks[i], embeddings[i]));
            }

            await _store.ReplaceFileAsync(relativePath, paired, prepared.Fingerprint, cancellationToken);
        }
    }
}
