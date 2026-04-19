using Akin.Core.Interfaces;
using Akin.Core.Models;
using VectorSharp.Embedding;
using VectorSharp.Embedding.NomicEmbed;

namespace Akin.Core.Services
{
    /// <summary>
    /// Top-level composition root holding all services for a single repository's
    /// index. Created once per process. Consumers receive only the interfaces
    /// they need rather than the whole context; the context exists so
    /// <see cref="OpenAsync"/> can tie construction and cleanup together.
    /// </summary>
    public sealed class RepoContext : IAsyncDisposable
    {
        private const string EmbeddingModelId = "nomic-embed-text-v1.5";
        private const string IndexFolderName = ".akin";

        public string RepoRoot { get; }
        public string IndexFolder { get; }
        public EmbeddingService Embedder { get; }
        public IChunkerSelector ChunkerSelector { get; }
        public IRepoScanner Scanner { get; }
        public IIndexStore Store { get; }
        public IIndexer Indexer { get; }
        public ISearchService SearchService { get; }
        public IndexReconciler Reconciler { get; }

        private RepoContext(
            string repoRoot,
            string indexFolder,
            EmbeddingService embedder,
            IChunkerSelector chunkerSelector,
            IRepoScanner scanner,
            IIndexStore store,
            IIndexer indexer,
            ISearchService searchService,
            IndexReconciler reconciler)
        {
            RepoRoot = repoRoot;
            IndexFolder = indexFolder;
            Embedder = embedder;
            ChunkerSelector = chunkerSelector;
            Scanner = scanner;
            Store = store;
            Indexer = indexer;
            SearchService = searchService;
            Reconciler = reconciler;
        }

        /// <summary>
        /// Opens the context for a repository, loading any existing index from disk.
        /// All construction is done inside a try/cleanup block so a failure in any
        /// step disposes the partially-constructed embedder and store.
        /// </summary>
        public static async Task<RepoContext> OpenAsync(string repoRoot, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

            EmbeddingService? embedder = null;
            IndexStore? store = null;

            try
            {
                string indexFolder = Path.Combine(repoRoot, IndexFolderName);

                // Resolve the Models folder via AppContext.BaseDirectory rather than
                // Assembly.Location. In single-file self-contained publishes
                // Assembly.Location returns an empty string, so the default
                // NomicEmbedProvider.Create() cannot find its model files. BaseDirectory
                // correctly points to the executable directory for single-file builds
                // and to the tool's bin folder for `dotnet tool install` deployments.
                string modelsDir = Path.Combine(AppContext.BaseDirectory, "Models");
                embedder = new EmbeddingService(() => NomicEmbedProvider.Create(modelsDir));
                IChunkerSelector chunkerSelector = new ChunkerSelector();
                IRepoScanner scanner = new RepoScanner(repoRoot);
                store = new IndexStore(indexFolder, embedder.Dimension);
                await store.OpenAsync(cancellationToken);

                FileChunker fileChunker = new FileChunker(repoRoot, chunkerSelector);
                IIndexer indexer = new Indexer(scanner, fileChunker, store, embedder, chunkerSelector, EmbeddingModelId);
                ISearchService searchService = new SearchService(embedder, store);
                IndexReconciler reconciler = new IndexReconciler(repoRoot, scanner, store, indexer);

                return new RepoContext(
                    repoRoot,
                    indexFolder,
                    embedder,
                    chunkerSelector,
                    scanner,
                    store,
                    indexer,
                    searchService,
                    reconciler);
            }
            catch
            {
                if (store != null) await store.DisposeAsync();
                if (embedder != null) await embedder.DisposeAsync();
                throw;
            }
        }

        /// <summary>
        /// Returns true if the current index is compatible with this context's
        /// configuration. An incompatible manifest (model or chunker change) means
        /// the index should be rebuilt.
        /// </summary>
        public bool IsIndexCompatible()
        {
            Manifest? manifest = Store.Manifest;
            if (manifest == null) return false;
            if (manifest.SchemaVersion != Manifest.CurrentSchemaVersion) return false;
            if (manifest.EmbeddingModel != EmbeddingModelId) return false;
            if (manifest.EmbeddingDimension != Embedder.Dimension) return false;
            if (manifest.ChunkerFingerprint != ChunkerSelector.Fingerprint) return false;
            return true;
        }

        /// <summary>
        /// Ensures the index is current. If no index exists or the manifest is
        /// incompatible, a full rebuild is performed. Otherwise, fingerprint-based
        /// reconciliation brings the index up to date with whatever changed since
        /// the last run.
        /// </summary>
        public async Task EnsureIndexReadyAsync(CancellationToken cancellationToken = default)
        {
            if (!Store.IsReady || !IsIndexCompatible())
            {
                await Indexer.ReindexAllAsync(cancellationToken);
                return;
            }

            await Reconciler.ReconcileAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            // Dispose in reverse order of dependency: store depends on nothing
            // local, embedder depends on nothing local, but any peripheral service
            // (scanner, chunker selector) might one day hold resources. Walk them
            // all so future replacements don't silently leak.
            await Store.DisposeAsync();
            await Embedder.DisposeAsync();
            await DisposeIfNeededAsync(Scanner);
            await DisposeIfNeededAsync(ChunkerSelector);
            await DisposeIfNeededAsync(Indexer);
            await DisposeIfNeededAsync(SearchService);
            await DisposeIfNeededAsync(Reconciler);
        }

        private static async ValueTask DisposeIfNeededAsync(object service)
        {
            if (service is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
