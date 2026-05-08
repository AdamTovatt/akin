using Akin.Core.Commands;
using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Tests
{
    public class SearchCommandTests
    {
        [Fact]
        public async Task ExecuteAsync_NoHits_ReturnsNoMatchesMessage()
        {
            StubSearchService searcher = new StubSearchService(Array.Empty<SearchHit>());
            SearchCommand command = new SearchCommand(searcher, NoopEnsureReady, "query", new SearchOptions());

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("No matches.", result.Message);
            Assert.Null(result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_SingleHitNoTruncation_OmitsOverflowLine()
        {
            SearchHit hit = new SearchHit
            {
                RelativePath = "src/foo.cs",
                AggregateScore = 0.9f,
                Regions = new[]
                {
                    new MatchedRegion { StartLine = 10, EndLine = 20, Score = 0.9f },
                },
            };
            StubSearchService searcher = new StubSearchService(new[] { hit });
            SearchCommand command = new SearchCommand(searcher, NoopEnsureReady, "query", new SearchOptions());

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.Contains("src/foo.cs  (1 match)", result.Details);
            Assert.Contains("lines 10-20", result.Details);
            Assert.DoesNotContain("not shown", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_TruncatedHit_RendersOverflowLineWithCorrectCount()
        {
            SearchHit hit = new SearchHit
            {
                RelativePath = "src/foo.cs",
                AggregateScore = 0.9f,
                Regions = new[]
                {
                    new MatchedRegion { StartLine = 1, EndLine = 5, Score = 0.95f },
                    new MatchedRegion { StartLine = 10, EndLine = 15, Score = 0.90f },
                    new MatchedRegion { StartLine = 20, EndLine = 25, Score = 0.85f },
                },
                TruncatedRegionCount = 4,
            };
            StubSearchService searcher = new StubSearchService(new[] { hit });
            SearchCommand command = new SearchCommand(searcher, NoopEnsureReady, "query", new SearchOptions());

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            // Header reports the total (3 visible + 4 truncated = 7).
            Assert.Contains("src/foo.cs  (7 matches)", result.Details);
            Assert.Contains("+4 more matches not shown", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_TruncatedByOne_UsesSingularInOverflowLine()
        {
            SearchHit hit = new SearchHit
            {
                RelativePath = "src/foo.cs",
                AggregateScore = 0.9f,
                Regions = new[]
                {
                    new MatchedRegion { StartLine = 1, EndLine = 5, Score = 0.9f },
                },
                TruncatedRegionCount = 1,
            };
            StubSearchService searcher = new StubSearchService(new[] { hit });
            SearchCommand command = new SearchCommand(searcher, NoopEnsureReady, "query", new SearchOptions());

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.Contains("+1 more match not shown", result.Details);
            Assert.DoesNotContain("+1 more matches", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_HitWithSnippet_IndentsSnippetLines()
        {
            SearchHit hit = new SearchHit
            {
                RelativePath = "src/foo.cs",
                AggregateScore = 0.9f,
                Regions = new[]
                {
                    new MatchedRegion
                    {
                        StartLine = 1,
                        EndLine = 2,
                        Score = 0.9f,
                        Snippet = "first line\nsecond line",
                    },
                },
            };
            StubSearchService searcher = new StubSearchService(new[] { hit });
            SearchCommand command = new SearchCommand(searcher, NoopEnsureReady, "query", new SearchOptions { IncludeSnippets = true });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.Contains("    first line", result.Details);
            Assert.Contains("    second line", result.Details);
        }

        private static Task NoopEnsureReady(IProgress<IndexProgress>? progress, CancellationToken cancellationToken)
            => Task.CompletedTask;

        private sealed class StubSearchService : ISearchService
        {
            private readonly IReadOnlyList<SearchHit> _hits;

            public StubSearchService(IReadOnlyList<SearchHit> hits)
            {
                _hits = hits;
            }

            public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchOptions options, CancellationToken cancellationToken = default)
                => Task.FromResult(_hits);
        }
    }
}
