using Akin.Core.Models;
using Akin.Core.Services;
using Akin.Tests.Fakes;
using VectorSharp.Embedding;

namespace Akin.Tests
{
    public class SearchServiceTests : IAsyncLifetime
    {
        private const int Dimension = 16;

        private string _tempFolder = string.Empty;
        private IndexStore _store = null!;
        private EmbeddingService _embedder = null!;
        private SearchService _searcher = null!;

        public async Task InitializeAsync()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "akin-search-" + Guid.NewGuid().ToString("N"));
            _store = new IndexStore(_tempFolder, Dimension);
            await _store.OpenAsync();

            _embedder = new EmbeddingService(() => new FakeEmbeddingProvider(Dimension));
            _searcher = new SearchService(_embedder, _store);
        }

        public async Task DisposeAsync()
        {
            await _embedder.DisposeAsync();
            await _store.DisposeAsync();
            try { Directory.Delete(_tempFolder, recursive: true); } catch { }
        }

        private async Task SeedAsync(params (string Path, int Start, int End, string Text)[] entries)
        {
            List<(ChunkDraft, float[])> built = new List<(ChunkDraft, float[])>();
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string path, int start, int end, string text) in entries)
            {
                float[] embedding = await _embedder.EmbedAsync(text, EmbeddingPurpose.Document);
                built.Add((new ChunkDraft
                {
                    RelativePath = path,
                    StartLine = start,
                    EndLine = end,
                    Text = text,
                }, embedding));
                paths.Add(path);
            }

            List<FileFingerprint> fingerprints = paths
                .Select(p => new FileFingerprint { RelativePath = p, Size = 1, ModifiedTicks = 1 })
                .ToList();

            Manifest manifest = new Manifest
            {
                SchemaVersion = Manifest.CurrentSchemaVersion,
                EmbeddingModel = "fake",
                EmbeddingDimension = Dimension,
                ChunkerFingerprint = "test",
                LastRebuiltUtc = DateTime.UtcNow,
            };

            await _store.ReplaceAllAsync(manifest, built, fingerprints);
        }

        [Fact]
        public async Task SearchAsync_EmptyIndex_ReturnsNoHits()
        {
            IReadOnlyList<SearchHit> hits = await _searcher.SearchAsync("anything", new SearchOptions());
            Assert.Empty(hits);
        }

        [Fact]
        public async Task SearchAsync_GroupsMultipleChunksIntoOneHit()
        {
            await SeedAsync(
                ("file.cs", 1, 10, "alpha beta gamma"),
                ("file.cs", 11, 20, "alpha beta delta")
            );

            IReadOnlyList<SearchHit> hits = await _searcher.SearchAsync("alpha beta", new SearchOptions { MaxResults = 5 });

            Assert.Single(hits);
            Assert.Equal("file.cs", hits[0].RelativePath);
        }

        [Fact]
        public async Task SearchAsync_AggregatesWithMaxScore()
        {
            await SeedAsync(
                ("a.cs", 1, 10, "alpha beta gamma"),
                ("a.cs", 11, 20, "completely different text"),
                ("b.cs", 1, 10, "alpha beta")
            );

            IReadOnlyList<SearchHit> hits = await _searcher.SearchAsync("alpha beta", new SearchOptions { MaxResults = 5 });

            Assert.NotEmpty(hits);
            foreach (SearchHit hit in hits)
            {
                float expectedMax = hit.Regions.Max(r => r.Score);
                Assert.Equal(expectedMax, hit.AggregateScore);
            }
        }

        [Fact]
        public async Task SearchAsync_IncludeSnippetsFalse_ReturnsRegionsWithoutText()
        {
            await SeedAsync(("a.cs", 1, 10, "alpha beta gamma"));

            IReadOnlyList<SearchHit> hits = await _searcher.SearchAsync("alpha beta", new SearchOptions { IncludeSnippets = false });

            Assert.Single(hits);
            Assert.All(hits[0].Regions, r => Assert.Null(r.Snippet));
        }

        [Fact]
        public async Task SearchAsync_RespectsMinimumScore()
        {
            await SeedAsync(
                ("a.cs", 1, 10, "matching"),
                ("b.cs", 1, 10, "completely orthogonal content that shares no tokens")
            );

            IReadOnlyList<SearchHit> hits = await _searcher.SearchAsync("matching", new SearchOptions
            {
                MinimumScore = 0.99f,
            });

            Assert.All(hits, hit => Assert.True(hit.AggregateScore >= 0.99f));
        }

        [Fact]
        public async Task SearchAsync_RespectsMaxResults()
        {
            List<(string, int, int, string)> entries = new List<(string, int, int, string)>();
            for (int i = 0; i < 10; i++)
            {
                entries.Add(($"file{i}.cs", 1, 5, $"content {i}"));
            }
            await SeedAsync(entries.ToArray());

            IReadOnlyList<SearchHit> hits = await _searcher.SearchAsync("content", new SearchOptions { MaxResults = 3 });
            Assert.True(hits.Count <= 3);
        }
    }
}
