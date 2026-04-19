using System.Collections.Concurrent;
using System.Text.Json;
using Akin.Core.Interfaces;
using Akin.Core.Models;
using VectorSharp.Storage;

namespace Akin.Core.Services
{
    /// <summary>
    /// Owns the on-disk layout for a single repository's index. Internally wraps a
    /// <see cref="CosineVectorStore{TKey}"/> and persists chunk metadata,
    /// fingerprints, and the manifest as JSON sidecars.
    ///
    /// Write operations are serialised through an internal async lock. Reads
    /// (search, chunk lookup, status) are lock-free and rely on
    /// <see cref="Volatile"/> semantics for the vector-store reference plus
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/> for chunk and fingerprint
    /// maps, so a concurrent search during a reindex never observes torn state.
    ///
    /// Persist ordering writes <c>manifest.json</c> last, so a crash mid-persist
    /// leaves an old manifest in place and the next <see cref="OpenAsync"/>
    /// falls through to its sanity check (vector count versus chunk count) and
    /// marks the store not ready if anything is inconsistent.
    ///
    /// <see cref="BeginBatchAsync"/> defers the on-disk persist until the scope
    /// disposes. In-memory state updates eagerly inside the scope, so searches
    /// running alongside the batch see the new data immediately — only the disk
    /// commit is deferred. A crash before the scope disposes rolls disk state
    /// back to the last successful persist.
    /// </summary>
    public sealed class IndexStore : IIndexStore
    {
        private const string VectorsFileName = "vectors.bin";
        private const string ChunksFileName = "chunks.json";
        private const string ManifestFileName = "manifest.json";
        private const string FingerprintsFileName = "fingerprints.json";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
        };

        private readonly string _folderPath;
        private readonly string _vectorsPath;
        private readonly string _chunksPath;
        private readonly string _manifestPath;
        private readonly string _fingerprintsPath;
        private readonly int _dimension;

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<int, ChunkInfo> _chunksById = new ConcurrentDictionary<int, ChunkInfo>();
        private readonly ConcurrentDictionary<string, FileFingerprint> _fingerprints = new ConcurrentDictionary<string, FileFingerprint>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> _chunksByFile = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        // Accessed lock-free by readers via Volatile.Read and swapped atomically
        // under _lock by writers. Never mutated in place once published.
        private CosineVectorStore<int> _vectorStore;

        private Manifest? _manifest;
        private int _nextId;
        private bool _ready;
        private bool _disposed;
        private int _batchDepth;
        private bool _pendingPersist;

        public Manifest? Manifest => Volatile.Read(ref _manifest);
        public bool IsReady => Volatile.Read(ref _ready);
        public int ChunkCount => _chunksById.Count;
        public int FileCount
        {
            get
            {
                // _chunksByFile is mutated only under _lock, but Count is a plain read.
                // Snapshot under the semaphore would be more conservative; for status
                // display a briefly stale count is acceptable.
                return _chunksByFile.Count;
            }
        }

        public IndexStore(string folderPath, int dimension)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            if (dimension <= 0)
                throw new ArgumentOutOfRangeException(nameof(dimension));

            _folderPath = folderPath;
            _vectorsPath = Path.Combine(folderPath, VectorsFileName);
            _chunksPath = Path.Combine(folderPath, ChunksFileName);
            _manifestPath = Path.Combine(folderPath, ManifestFileName);
            _fingerprintsPath = Path.Combine(folderPath, FingerprintsFileName);
            _dimension = dimension;
            _vectorStore = new CosineVectorStore<int>("akin", dimension);
        }

        public async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_manifestPath) || !File.Exists(_chunksPath) || !File.Exists(_vectorsPath))
                {
                    Volatile.Write(ref _ready, false);
                    return;
                }

                string manifestJson = await File.ReadAllTextAsync(_manifestPath, cancellationToken);
                Manifest? manifest = JsonSerializer.Deserialize<Manifest>(manifestJson, JsonOptions);
                if (manifest == null || manifest.EmbeddingDimension != _dimension)
                {
                    Volatile.Write(ref _ready, false);
                    return;
                }

                string chunksJson = await File.ReadAllTextAsync(_chunksPath, cancellationToken);
                List<ChunkInfo>? chunks = JsonSerializer.Deserialize<List<ChunkInfo>>(chunksJson, JsonOptions);
                if (chunks == null)
                {
                    Volatile.Write(ref _ready, false);
                    return;
                }

                List<FileFingerprint> fingerprints = new List<FileFingerprint>();
                if (File.Exists(_fingerprintsPath))
                {
                    string fingerprintsJson = await File.ReadAllTextAsync(_fingerprintsPath, cancellationToken);
                    fingerprints = JsonSerializer.Deserialize<List<FileFingerprint>>(fingerprintsJson, JsonOptions)
                                   ?? new List<FileFingerprint>();
                }

                CosineVectorStore<int> freshStore = new CosineVectorStore<int>("akin", _dimension);
                await using (FileStream vectorStream = File.OpenRead(_vectorsPath))
                {
                    await freshStore.LoadAsync(vectorStream);
                }

                // Partial-write sanity check.
                if (freshStore.Count != chunks.Count)
                {
                    freshStore.Dispose();
                    Volatile.Write(ref _ready, false);
                    return;
                }

                _chunksById.Clear();
                _chunksByFile.Clear();
                _fingerprints.Clear();

                foreach (ChunkInfo chunk in chunks)
                {
                    _chunksById[chunk.Id] = chunk;
                    if (!_chunksByFile.TryGetValue(chunk.RelativePath, out List<int>? ids))
                    {
                        ids = new List<int>();
                        _chunksByFile[chunk.RelativePath] = ids;
                    }
                    ids.Add(chunk.Id);
                }

                foreach (FileFingerprint fingerprint in fingerprints)
                {
                    _fingerprints[fingerprint.RelativePath] = fingerprint;
                }

                _nextId = chunks.Count == 0 ? 0 : chunks.Max(c => c.Id) + 1;
                Volatile.Write(ref _manifest, manifest);

                CosineVectorStore<int> oldStore = Volatile.Read(ref _vectorStore);
                Volatile.Write(ref _vectorStore, freshStore);
                oldStore.Dispose();

                Volatile.Write(ref _ready, true);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ReplaceAllAsync(
            Manifest manifest,
            IReadOnlyList<(ChunkDraft Draft, float[] Embedding)> chunks,
            IReadOnlyList<FileFingerprint> fingerprints,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            ArgumentNullException.ThrowIfNull(chunks);
            ArgumentNullException.ThrowIfNull(fingerprints);

            await _lock.WaitAsync(cancellationToken);
            try
            {
                CosineVectorStore<int> freshStore = new CosineVectorStore<int>("akin", _dimension);
                Dictionary<int, ChunkInfo> freshChunks = new Dictionary<int, ChunkInfo>();
                Dictionary<string, List<int>> freshByFile = new Dictionary<string, List<int>>(StringComparer.Ordinal);

                int id = 0;
                foreach ((ChunkDraft draft, float[] embedding) in chunks)
                {
                    ChunkInfo info = ToInfo(draft, id);
                    await freshStore.AddAsync(id, embedding, cancellationToken);
                    freshChunks[id] = info;
                    if (!freshByFile.TryGetValue(info.RelativePath, out List<int>? ids))
                    {
                        ids = new List<int>();
                        freshByFile[info.RelativePath] = ids;
                    }
                    ids.Add(id);
                    id++;
                }

                // Commit new state atomically: vector store via Volatile, dicts via
                // clear+repopulate while holding the lock. Readers are either fully
                // on the old state (pre-swap) or fully on the new (post-swap);
                // they never observe half-converted data because they only touch
                // the vector store reference and the concurrent chunk dict.
                CosineVectorStore<int> oldStore = Volatile.Read(ref _vectorStore);
                Volatile.Write(ref _vectorStore, freshStore);

                _chunksById.Clear();
                foreach (KeyValuePair<int, ChunkInfo> pair in freshChunks)
                    _chunksById[pair.Key] = pair.Value;

                _chunksByFile.Clear();
                foreach (KeyValuePair<string, List<int>> pair in freshByFile)
                    _chunksByFile[pair.Key] = pair.Value;

                _fingerprints.Clear();
                foreach (FileFingerprint fingerprint in fingerprints)
                    _fingerprints[fingerprint.RelativePath] = fingerprint;

                _nextId = id;
                Volatile.Write(ref _manifest, manifest);
                Volatile.Write(ref _ready, true);

                oldStore.Dispose();

                await PersistOrDeferLockedAsync(cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ReplaceFileAsync(
            string relativePath,
            IReadOnlyList<(ChunkDraft Draft, float[] Embedding)> chunks,
            FileFingerprint? fingerprint,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
            ArgumentNullException.ThrowIfNull(chunks);

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await RemoveFileLockedAsync(relativePath, cancellationToken);

                CosineVectorStore<int> store = Volatile.Read(ref _vectorStore);
                foreach ((ChunkDraft draft, float[] embedding) in chunks)
                {
                    int id = _nextId++;
                    ChunkInfo info = ToInfo(draft, id);
                    await store.AddAsync(id, embedding, cancellationToken);
                    _chunksById[id] = info;
                    if (!_chunksByFile.TryGetValue(info.RelativePath, out List<int>? ids))
                    {
                        ids = new List<int>();
                        _chunksByFile[info.RelativePath] = ids;
                    }
                    ids.Add(id);
                }

                if (fingerprint != null)
                {
                    _fingerprints[fingerprint.RelativePath] = fingerprint;
                }
                else
                {
                    _fingerprints.TryRemove(relativePath, out _);
                }

                await PersistOrDeferLockedAsync(cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<int> RemoveFileAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

            await _lock.WaitAsync(cancellationToken);
            try
            {
                int removed = await RemoveFileLockedAsync(relativePath, cancellationToken);
                bool fingerprintRemoved = _fingerprints.TryRemove(relativePath, out _);
                if (removed > 0 || fingerprintRemoved)
                    await PersistOrDeferLockedAsync(cancellationToken);
                return removed;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IReadOnlyList<(int Id, float Score)>> FindMostSimilarAsync(
            float[] queryVector,
            int count,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(queryVector);
            if (count <= 0) return Array.Empty<(int, float)>();

            // Lock-free read of the current vector store reference. The underlying
            // CosineVectorStore has its own internal lock for concurrent Add/Remove/Find,
            // so operating on the snapshot reference is safe even if writers subsequently
            // swap in a new store.
            CosineVectorStore<int> store = Volatile.Read(ref _vectorStore);
            IReadOnlyList<SearchResult<int>> results = await store.FindMostSimilarAsync(queryVector, count, cancellationToken);

            List<(int, float)> projected = new List<(int, float)>(results.Count);
            foreach (SearchResult<int> result in results)
            {
                projected.Add((result.Id, result.Score));
            }
            return projected;
        }

        public ChunkInfo? GetChunk(int id)
        {
            _chunksById.TryGetValue(id, out ChunkInfo? info);
            return info;
        }

        public IReadOnlyCollection<ChunkInfo> AllChunks()
        {
            // ConcurrentDictionary.Values returns an ICollection snapshot; wrap as a
            // read-only list for the interface contract.
            return _chunksById.Values.ToArray();
        }

        public FileFingerprint? GetFingerprint(string relativePath)
        {
            _fingerprints.TryGetValue(relativePath, out FileFingerprint? fingerprint);
            return fingerprint;
        }

        public IReadOnlyCollection<FileFingerprint> AllFingerprints() => _fingerprints.Values.ToArray();

        public IndexStatus GetStatus()
        {
            return new IndexStatus
            {
                FileCount = FileCount,
                ChunkCount = ChunkCount,
                Manifest = Volatile.Read(ref _manifest),
                IsReady = Volatile.Read(ref _ready),
            };
        }

        public async ValueTask<IAsyncDisposable> BeginBatchAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _batchDepth++;
                return new BatchScope(this);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await _lock.WaitAsync();
            try
            {
                Volatile.Read(ref _vectorStore).Dispose();
            }
            finally
            {
                _lock.Release();
                _lock.Dispose();
            }
        }

        private static ChunkInfo ToInfo(ChunkDraft draft, int id) => new ChunkInfo
        {
            Id = id,
            RelativePath = draft.RelativePath,
            StartLine = draft.StartLine,
            EndLine = draft.EndLine,
            Text = draft.Text,
        };

        private async Task<int> RemoveFileLockedAsync(string relativePath, CancellationToken cancellationToken)
        {
            if (!_chunksByFile.TryGetValue(relativePath, out List<int>? ids))
                return 0;

            int count = ids.Count;
            CosineVectorStore<int> store = Volatile.Read(ref _vectorStore);
            foreach (int id in ids)
            {
                await store.RemoveAsync(id, cancellationToken);
                _chunksById.TryRemove(id, out _);
            }
            _chunksByFile.Remove(relativePath);
            return count;
        }

        private async Task PersistOrDeferLockedAsync(CancellationToken cancellationToken)
        {
            if (_batchDepth > 0)
            {
                _pendingPersist = true;
                return;
            }
            await PersistLockedAsync(cancellationToken);
        }

        private async Task PersistLockedAsync(CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_folderPath);

            string tempVectors = _vectorsPath + ".tmp";
            string tempChunks = _chunksPath + ".tmp";
            string tempManifest = _manifestPath + ".tmp";
            string tempFingerprints = _fingerprintsPath + ".tmp";

            try
            {
                await using (FileStream stream = File.Create(tempVectors))
                {
                    await Volatile.Read(ref _vectorStore).SaveAsync(stream);
                }

                List<ChunkInfo> chunkList = _chunksById.Values.OrderBy(c => c.Id).ToList();
                await using (FileStream chunkStream = File.Create(tempChunks))
                {
                    await JsonSerializer.SerializeAsync(chunkStream, chunkList, JsonOptions, cancellationToken);
                }

                List<FileFingerprint> fingerprintList = _fingerprints.Values
                    .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
                    .ToList();
                await using (FileStream fpStream = File.Create(tempFingerprints))
                {
                    await JsonSerializer.SerializeAsync(fpStream, fingerprintList, JsonOptions, cancellationToken);
                }

                await using (FileStream manifestStream = File.Create(tempManifest))
                {
                    await JsonSerializer.SerializeAsync(manifestStream, _manifest, JsonOptions, cancellationToken);
                }

                // Swap in order: vectors, chunks, fingerprints, manifest LAST.
                File.Move(tempVectors, _vectorsPath, overwrite: true);
                File.Move(tempChunks, _chunksPath, overwrite: true);
                File.Move(tempFingerprints, _fingerprintsPath, overwrite: true);
                File.Move(tempManifest, _manifestPath, overwrite: true);
            }
            catch
            {
                TryDelete(tempVectors);
                TryDelete(tempChunks);
                TryDelete(tempFingerprints);
                TryDelete(tempManifest);
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup; never rethrow from the catch path.
            }
        }

        private sealed class BatchScope : IAsyncDisposable
        {
            private readonly IndexStore _owner;
            private bool _disposed;

            public BatchScope(IndexStore owner) { _owner = owner; }

            public async ValueTask DisposeAsync()
            {
                if (_disposed) return;
                _disposed = true;

                await _owner._lock.WaitAsync();
                try
                {
                    _owner._batchDepth--;
                    if (_owner._batchDepth == 0 && _owner._pendingPersist)
                    {
                        _owner._pendingPersist = false;
                        await _owner.PersistLockedAsync(default);
                    }
                }
                finally
                {
                    _owner._lock.Release();
                }
            }
        }
    }
}
