using Akin.Core.Models;
using Akin.Core.Services;

namespace Akin.Tests
{
    public class FileChunkerTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly FileChunker _chunker;

        public FileChunkerTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "akin-filechunker-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
            _chunker = new FileChunker(_tempRoot, new ChunkerSelector());
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        }

        private string WriteFile(string relativePath, string contents)
        {
            string absolute = Path.Combine(_tempRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, contents);
            return absolute;
        }

        [Fact]
        public async Task PrepareAsync_MissingFile_ReturnsNull()
        {
            PreparedFile? result = await _chunker.PrepareAsync("nonexistent.md");
            Assert.Null(result);
        }

        [Fact]
        public async Task PrepareAsync_EmptyFile_ReturnsFingerprintAndNoChunks()
        {
            WriteFile("empty.md", string.Empty);
            PreparedFile? result = await _chunker.PrepareAsync("empty.md");
            Assert.NotNull(result);
            Assert.Empty(result.Chunks);
        }

        [Fact]
        public async Task PrepareAsync_BinaryContentInAllowlistedExtension_IsSkipped()
        {
            // A file with a text-file extension but null bytes in the content
            // should be skipped by the binary detector, not chunked as text.
            string absolute = Path.Combine(_tempRoot, "poisoned.txt");
            byte[] content = System.Text.Encoding.UTF8.GetBytes("Some leading ASCII\n");
            byte[] payload = new byte[content.Length + 4];
            content.CopyTo(payload, 0);
            payload[content.Length] = 0x00;
            payload[content.Length + 1] = 0xFF;
            payload[content.Length + 2] = 0x42;
            payload[content.Length + 3] = 0x13;
            File.WriteAllBytes(absolute, payload);

            PreparedFile? result = await _chunker.PrepareAsync("poisoned.txt");
            Assert.NotNull(result);
            Assert.Empty(result.Chunks);
        }

        [Fact]
        public async Task PrepareAsync_AssetExtension_EmitsFilenameOnlyChunk()
        {
            // .ai is on the filename-only allowlist. The content isn't chunked,
            // but we still emit one synthetic chunk so the file can be found by
            // name through semantic search.
            WriteFile("Art/Logo.ai", "%PDF-1.6\nbinary payload that we don't index");

            PreparedFile? result = await _chunker.PrepareAsync("Art/Logo.ai");

            Assert.NotNull(result);
            Assert.Single(result.Chunks);

            ChunkDraft only = result.Chunks[0];
            Assert.Equal("Art/Logo.ai", only.RelativePath);
            Assert.Equal(1, only.StartLine);
            Assert.Equal(1, only.EndLine);
            Assert.Contains("Art/Logo.ai", only.Text);
            Assert.Equal("Art/Logo.ai", only.EmbeddingText);
        }

        [Fact]
        public async Task PrepareAsync_TrulyUnknownExtension_ReturnsNoChunks()
        {
            WriteFile("data.mystery", "contents that we don't understand");
            PreparedFile? result = await _chunker.PrepareAsync("data.mystery");
            Assert.NotNull(result);
            Assert.Empty(result.Chunks);
        }

        [Fact]
        public async Task PrepareAsync_SingleLineTextFile_AssignsCorrectLineRange()
        {
            WriteFile("sample.txt", "just one line of text");

            PreparedFile? result = await _chunker.PrepareAsync("sample.txt");
            Assert.NotNull(result);
            Assert.Single(result.Chunks);
            Assert.Equal(1, result.Chunks[0].StartLine);
            Assert.Equal(1, result.Chunks[0].EndLine);
        }

        [Fact]
        public async Task PrepareAsync_ThreeLinesWithTrailingNewline_SingleChunkCoversAll()
        {
            WriteFile("three.md", "line 1\nline 2\nline 3\n");

            PreparedFile? result = await _chunker.PrepareAsync("three.md");
            Assert.NotNull(result);
            Assert.Single(result.Chunks);
            Assert.Equal(1, result.Chunks[0].StartLine);
            Assert.Equal(3, result.Chunks[0].EndLine);
        }

        [Fact]
        public async Task PrepareAsync_RoundTripConcatenationMatchesSource()
        {
            string source = "# Title\n\nFirst paragraph with several sentences. Second sentence here! A question?\n\n## Section\n\n- one\n- two\n\nFinal paragraph.\n";
            WriteFile("doc.md", source);

            PreparedFile? result = await _chunker.PrepareAsync("doc.md");
            Assert.NotNull(result);
            Assert.NotEmpty(result.Chunks);

            string reconstructed = string.Concat(result.Chunks.Select(c => c.Text));
            Assert.Equal(source, reconstructed);
        }

        [Fact]
        public async Task PrepareAsync_MultipleChunks_HaveNonDecreasingStartLines()
        {
            // Force chunking by writing a long file with clear paragraph breaks.
            string source = string.Join(
                "\n\n",
                Enumerable.Range(1, 40).Select(i => $"Paragraph {i} with content that fills roughly one paragraph's worth of text."));
            WriteFile("long.md", source + "\n");

            PreparedFile? result = await _chunker.PrepareAsync("long.md");
            Assert.NotNull(result);
            Assert.True(result.Chunks.Count >= 2, $"Expected multiple chunks, got {result.Chunks.Count}.");

            for (int i = 1; i < result.Chunks.Count; i++)
            {
                ChunkDraft prev = result.Chunks[i - 1];
                ChunkDraft curr = result.Chunks[i];
                Assert.True(curr.StartLine >= prev.StartLine, $"Chunk {i} starts before chunk {i - 1}.");
                Assert.True(curr.EndLine >= curr.StartLine, $"Chunk {i} has end line before start line.");
            }
        }

        [Fact]
        public async Task PrepareAsync_OversizedFile_IsSkipped()
        {
            FileChunker smallLimitChunker = new FileChunker(_tempRoot, new ChunkerSelector(), maxFileSizeBytes: 10);
            WriteFile("big.md", "this file is longer than ten bytes");

            PreparedFile? result = await smallLimitChunker.PrepareAsync("big.md");
            Assert.NotNull(result);
            Assert.Empty(result.Chunks);
            Assert.True(result.Fingerprint.Size > 10);
        }
    }
}
