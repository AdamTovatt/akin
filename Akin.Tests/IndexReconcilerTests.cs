using Akin.Core.Models;
using Akin.Core.Services;
using Akin.Tests.Fakes;

namespace Akin.Tests
{
    public class IndexReconcilerTests : IAsyncLifetime
    {
        private const int Dimension = 4;

        private string _repoRoot = string.Empty;
        private string _indexFolder = string.Empty;
        private IndexStore _store = null!;
        private StubScanner _scanner = null!;
        private StubIndexer _indexer = null!;
        private IndexReconciler _reconciler = null!;

        public async Task InitializeAsync()
        {
            _repoRoot = Path.Combine(Path.GetTempPath(), "akin-reconcile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_repoRoot);
            _indexFolder = Path.Combine(_repoRoot, ".akin");

            _store = new IndexStore(_indexFolder, Dimension);
            await _store.OpenAsync();

            _scanner = new StubScanner();
            _indexer = new StubIndexer();
            _reconciler = new IndexReconciler(_repoRoot, _scanner, _store, _indexer);
        }

        public async Task DisposeAsync()
        {
            await _store.DisposeAsync();
            try { Directory.Delete(_repoRoot, recursive: true); } catch { }
        }

        private string WriteFile(string relativePath, string contents)
        {
            string absolute = Path.Combine(_repoRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, contents);
            return absolute;
        }

        private async Task SeedIndexAsync(params (string Path, string Contents)[] files)
        {
            List<FileFingerprint> fingerprints = new List<FileFingerprint>();
            foreach ((string path, string contents) in files)
            {
                string absolute = WriteFile(path, contents);
                fingerprints.Add(FileFingerprint.FromFile(path, new FileInfo(absolute)));
            }

            Manifest manifest = new Manifest
            {
                SchemaVersion = Manifest.CurrentSchemaVersion,
                EmbeddingModel = "fake",
                EmbeddingDimension = Dimension,
                ChunkerFingerprint = "test",
                LastRebuiltUtc = DateTime.UtcNow,
            };

            await _store.ReplaceAllAsync(manifest, Array.Empty<(ChunkDraft, float[])>(), fingerprints);
        }

        [Fact]
        public async Task ReconcileAsync_NoChanges_DoesNothing()
        {
            await SeedIndexAsync(("a.cs", "content a"), ("b.cs", "content b"));
            _scanner.Files = new List<string> { "a.cs", "b.cs" };

            ReconciliationResult result = await _reconciler.ReconcileAsync();

            Assert.Equal(0, result.FilesReindexed);
            Assert.Equal(0, result.FilesRemoved);
            Assert.Empty(_indexer.ReindexedFiles);
        }

        [Fact]
        public async Task ReconcileAsync_NewFile_TriggersReindex()
        {
            await SeedIndexAsync(("a.cs", "content a"));

            WriteFile("b.cs", "newly created");
            _scanner.Files = new List<string> { "a.cs", "b.cs" };

            ReconciliationResult result = await _reconciler.ReconcileAsync();

            Assert.Equal(1, result.FilesReindexed);
            Assert.Contains("b.cs", _indexer.ReindexedFiles);
        }

        [Fact]
        public async Task ReconcileAsync_ModifiedFile_TriggersReindex()
        {
            await SeedIndexAsync(("a.cs", "original content"));
            _scanner.Files = new List<string> { "a.cs" };

            string absolute = Path.Combine(_repoRoot, "a.cs");
            File.WriteAllText(absolute, "new content — noticeably longer");
            File.SetLastWriteTimeUtc(absolute, DateTime.UtcNow.AddMinutes(5));

            ReconciliationResult result = await _reconciler.ReconcileAsync();

            Assert.Equal(1, result.FilesReindexed);
            Assert.Contains("a.cs", _indexer.ReindexedFiles);
        }

        [Fact]
        public async Task ReconcileAsync_UntrackedFile_IsRemoved()
        {
            await SeedIndexAsync(("a.cs", "keep me"), ("b.cs", "remove me"));
            _scanner.Files = new List<string> { "a.cs" };

            ReconciliationResult result = await _reconciler.ReconcileAsync();

            Assert.Equal(1, result.FilesRemoved);
            Assert.Null(_store.GetFingerprint("b.cs"));
            Assert.NotNull(_store.GetFingerprint("a.cs"));
        }

        [Fact]
        public async Task ReconcileAsync_TrackedButMissingOnDisk_IsRemoved()
        {
            await SeedIndexAsync(("a.cs", "content a"));
            _scanner.Files = new List<string> { "a.cs" };

            File.Delete(Path.Combine(_repoRoot, "a.cs"));

            ReconciliationResult result = await _reconciler.ReconcileAsync();

            Assert.Equal(1, result.FilesRemoved);
            Assert.Null(_store.GetFingerprint("a.cs"));
        }
    }
}
