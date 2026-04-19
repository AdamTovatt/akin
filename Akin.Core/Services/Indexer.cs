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
        private readonly IRepoScanner _scanner;
        private readonly FileChunker _fileChunker;
        private readonly IIndexStore _store;
        private readonly EmbeddingService _embedder;
        private readonly IChunkerSelector _chunkerSelector;
        private readonly string _embeddingModelId;

        public Indexer(
            IRepoScanner scanner,
            FileChunker fileChunker,
            IIndexStore store,
            EmbeddingService embedder,
            IChunkerSelector chunkerSelector,
            string embeddingModelId)
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
        }

        public async Task ReindexAllAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> files = await _scanner.ScanAsync(cancellationToken);

            List<(ChunkDraft Draft, float[] Embedding)> allChunks = new List<(ChunkDraft, float[])>();
            List<FileFingerprint> fingerprints = new List<FileFingerprint>();

            foreach (string relativePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                PreparedFile? prepared = await _fileChunker.PrepareAsync(relativePath, cancellationToken);
                if (prepared == null) continue;

                fingerprints.Add(prepared.Fingerprint);
                if (prepared.Chunks.Count == 0) continue;

                IReadOnlyList<string> texts = prepared.Chunks.Select(c => c.Text).ToList();
                float[][] embeddings = await _embedder.EmbedBatchAsync(texts, EmbeddingPurpose.Document, cancellationToken);

                for (int i = 0; i < prepared.Chunks.Count; i++)
                {
                    allChunks.Add((prepared.Chunks[i], embeddings[i]));
                }
            }

            Manifest manifest = new Manifest
            {
                SchemaVersion = Manifest.CurrentSchemaVersion,
                EmbeddingModel = _embeddingModelId,
                EmbeddingDimension = _embedder.Dimension,
                ChunkerFingerprint = _chunkerSelector.Fingerprint,
                LastRebuiltUtc = DateTime.UtcNow,
            };

            await _store.ReplaceAllAsync(manifest, allChunks, fingerprints, cancellationToken);
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
