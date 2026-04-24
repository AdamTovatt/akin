using Akin.Core.Models;
using Akin.Core.Services;

namespace Akin.Tests
{
    public class IndexStoreTests : IAsyncLifetime
    {
        private const int Dimension = 8;

        private string _tempFolder = string.Empty;
        private IndexStore _store = null!;

        public async Task InitializeAsync()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "akin-store-" + Guid.NewGuid().ToString("N"));
            _store = new IndexStore(_tempFolder, Dimension);
            await _store.OpenAsync();
        }

        public async Task DisposeAsync()
        {
            await _store.DisposeAsync();
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { }
        }

        private static Manifest BuildManifest() => new Manifest
        {
            SchemaVersion = Manifest.CurrentSchemaVersion,
            EmbeddingModel = "fake",
            EmbeddingDimension = Dimension,
            ChunkerFingerprint = "test",
            LastIndexUpdateUtc = DateTime.UtcNow,
        };

        private static (ChunkDraft, float[]) MakeEntry(int vectorSlot, string path, int startLine, int endLine, string text)
        {
            ChunkDraft draft = new ChunkDraft
            {
                RelativePath = path,
                StartLine = startLine,
                EndLine = endLine,
                Text = text,
            };
            float[] vector = new float[Dimension];
            vector[vectorSlot % Dimension] = 1.0f;
            return (draft, vector);
        }

        private static FileFingerprint MakeFingerprint(string path, long size = 100, long ticks = 1000)
        {
            return new FileFingerprint
            {
                RelativePath = path,
                Size = size,
                ModifiedTicks = ticks,
            };
        }

        [Fact]
        public async Task ReplaceAllAsync_MakesStoreReadyAndPersists()
        {
            await _store.ReplaceAllAsync(BuildManifest(), new[]
            {
                MakeEntry(0, "a.cs", 1, 10, "chunk a"),
                MakeEntry(1, "b.cs", 1, 20, "chunk b"),
            }, new[]
            {
                MakeFingerprint("a.cs"),
                MakeFingerprint("b.cs"),
            });

            Assert.True(_store.IsReady);
            Assert.Equal(2, _store.ChunkCount);
            Assert.Equal(2, _store.FileCount);

            Assert.True(File.Exists(Path.Combine(_tempFolder, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(_tempFolder, "chunks.json")));
            Assert.True(File.Exists(Path.Combine(_tempFolder, "vectors.bin")));
            Assert.True(File.Exists(Path.Combine(_tempFolder, "fingerprints.json")));
        }

        [Fact]
        public async Task OpenAsync_ReloadsAfterRestart()
        {
            await _store.ReplaceAllAsync(BuildManifest(), new[]
            {
                MakeEntry(0, "a.cs", 1, 10, "chunk a"),
                MakeEntry(1, "a.cs", 11, 20, "chunk a2"),
                MakeEntry(2, "b.cs", 1, 5, "chunk b"),
            }, new[]
            {
                MakeFingerprint("a.cs", size: 500, ticks: 12345),
                MakeFingerprint("b.cs", size: 50, ticks: 67890),
            });

            await _store.DisposeAsync();

            IndexStore reopened = new IndexStore(_tempFolder, Dimension);
            await reopened.OpenAsync();

            Assert.True(reopened.IsReady);
            Assert.Equal(3, reopened.ChunkCount);
            Assert.Equal(2, reopened.FileCount);

            FileFingerprint? roundTripped = reopened.GetFingerprint("a.cs");
            Assert.NotNull(roundTripped);
            Assert.Equal(500, roundTripped.Size);
            Assert.Equal(12345, roundTripped.ModifiedTicks);

            await reopened.DisposeAsync();

            _store = new IndexStore(_tempFolder, Dimension);
            await _store.OpenAsync();
        }

        [Fact]
        public async Task ReplaceFileAsync_ReplacesOnlyThatFile()
        {
            await _store.ReplaceAllAsync(BuildManifest(), new[]
            {
                MakeEntry(0, "a.cs", 1, 10, "chunk a1"),
                MakeEntry(1, "a.cs", 11, 20, "chunk a2"),
                MakeEntry(2, "b.cs", 1, 5, "chunk b"),
            }, new[]
            {
                MakeFingerprint("a.cs"),
                MakeFingerprint("b.cs"),
            });

            // Capture "a.cs" chunk ids before replacement so we can verify they vanish.
            int[] aIdsBefore = _store.AllChunks()
                .Where(c => c.RelativePath == "a.cs")
                .Select(c => c.Id)
                .ToArray();

            await _store.ReplaceFileAsync("a.cs", new[]
            {
                MakeEntry(3, "a.cs", 1, 5, "new a1"),
            }, MakeFingerprint("a.cs", size: 200, ticks: 99999));

            Assert.Equal(2, _store.ChunkCount);
            Assert.Equal(2, _store.FileCount);

            foreach (int oldId in aIdsBefore)
            {
                Assert.Null(_store.GetChunk(oldId));
            }

            int newACount = _store.AllChunks().Count(c => c.RelativePath == "a.cs");
            Assert.Equal(1, newACount);

            FileFingerprint? updated = _store.GetFingerprint("a.cs");
            Assert.NotNull(updated);
            Assert.Equal(200, updated.Size);
            Assert.Equal(99999, updated.ModifiedTicks);
        }

        [Fact]
        public async Task ReplaceFileAsync_NullFingerprint_ClearsExisting()
        {
            await _store.ReplaceAllAsync(BuildManifest(), new[]
            {
                MakeEntry(0, "a.cs", 1, 10, "chunk a"),
            }, new[]
            {
                MakeFingerprint("a.cs"),
            });

            await _store.ReplaceFileAsync("a.cs", Array.Empty<(ChunkDraft, float[])>(), null);

            Assert.Null(_store.GetFingerprint("a.cs"));
        }

        [Fact]
        public async Task RemoveFileAsync_ReturnsCountAndDropsChunks()
        {
            await _store.ReplaceAllAsync(BuildManifest(), new[]
            {
                MakeEntry(0, "a.cs", 1, 10, "chunk a1"),
                MakeEntry(1, "a.cs", 11, 20, "chunk a2"),
                MakeEntry(2, "b.cs", 1, 5, "chunk b"),
            }, new[]
            {
                MakeFingerprint("a.cs"),
                MakeFingerprint("b.cs"),
            });

            int removed = await _store.RemoveFileAsync("a.cs");
            Assert.Equal(2, removed);
            Assert.Equal(1, _store.ChunkCount);
            Assert.Equal(1, _store.FileCount);
            Assert.Null(_store.GetFingerprint("a.cs"));
        }

        [Fact]
        public async Task RemoveFileAsync_MissingFile_ReturnsZero()
        {
            await _store.ReplaceAllAsync(BuildManifest(), new[]
            {
                MakeEntry(0, "a.cs", 1, 10, "chunk a"),
            }, new[]
            {
                MakeFingerprint("a.cs"),
            });

            int removed = await _store.RemoveFileAsync("nonexistent.cs");
            Assert.Equal(0, removed);
            Assert.Equal(1, _store.ChunkCount);
        }

        [Fact]
        public async Task BeginBatch_DefersPersistUntilDisposal()
        {
            string vectorsPath = Path.Combine(_tempFolder, "vectors.bin");

            await using (IAsyncDisposable batch = await _store.BeginBatchAsync())
            {
                await _store.ReplaceAllAsync(BuildManifest(), new[]
                {
                    MakeEntry(0, "a.cs", 1, 10, "chunk a"),
                }, new[]
                {
                    MakeFingerprint("a.cs"),
                });

                // No persist happens while we are inside the batch scope.
                Assert.False(File.Exists(vectorsPath));
            }

            // On disposal, the pending persist flushes.
            Assert.True(File.Exists(vectorsPath));
        }

        [Fact]
        public async Task FindMostSimilarAsync_ChunkFilter_SurvivesConcurrentReindex()
        {
            // Regression test for the race where a pre-computed chunk-id allow-set
            // is built from one snapshot and consumed against a newer snapshot. A
            // full reindex resets ids to zero, so a stale allow-set computed against
            // the previous snapshot could admit ids in the new snapshot that refer
            // to different files. The IndexSnapshot refactor makes the filter and
            // the scan consume the same immutable snapshot, so this test drives
            // many searches in parallel with repeated reindexes and asserts that
            // every hit matches the filter we passed in.

            Manifest manifest = BuildManifest();

            string[] paths = new[] { "src/a.cs", "src/b.cs", "tests/a.cs", "tests/b.cs" };
            FileFingerprint[] fingerprints = paths.Select(p => MakeFingerprint(p)).ToArray();

            async Task ReindexAsync(int seedOffset)
            {
                (ChunkDraft, float[])[] chunks = paths
                    .Select((p, i) => MakeEntry(i + seedOffset, p, 1, 10, $"text {p} {seedOffset}"))
                    .ToArray();
                await _store.ReplaceAllAsync(manifest, chunks, fingerprints);
            }

            await ReindexAsync(0);

            float[] query = new float[Dimension];
            query[0] = 1.0f;

            Func<ChunkInfo, bool> filter = c => c.RelativePath.StartsWith("src/");

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            int reindexCount = 0;

            Task reindexer = Task.Run(async () =>
            {
                int seed = 1;
                while (!cts.IsCancellationRequested)
                {
                    await ReindexAsync(seed++);
                    Interlocked.Increment(ref reindexCount);
                }
            });

            int searchCount = 0;
            int violations = 0;
            Task searcher = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    IReadOnlyList<(ChunkInfo Chunk, float Score)> hits =
                        await _store.FindMostSimilarAsync(query, 10, filter);
                    foreach ((ChunkInfo chunk, float _) in hits)
                    {
                        if (!chunk.RelativePath.StartsWith("src/"))
                            Interlocked.Increment(ref violations);
                    }
                    Interlocked.Increment(ref searchCount);
                }
            });

            await Task.WhenAll(reindexer, searcher);

            Assert.True(reindexCount > 5, $"Expected the reindexer to fire many times; got {reindexCount}.");
            Assert.True(searchCount > 5, $"Expected the searcher to run many times; got {searchCount}.");
            Assert.Equal(0, violations);
        }
    }
}
