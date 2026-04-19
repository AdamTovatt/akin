using Akin.Core.Interfaces;
using Akin.Core.Models;
using VectorSharp.Chunking;

namespace Akin.Core.Services
{
    /// <summary>
    /// Turns a single file on disk into a list of <see cref="ChunkDraft"/>s plus
    /// a <see cref="FileFingerprint"/>. Handles size and binary filtering, picks
    /// a chunker config based on file extension, and tracks line numbers as the
    /// chunker emits segments. Kept separate from <see cref="Indexer"/> so the
    /// indexer can stay focused on orchestration (scan, embed, store).
    /// </summary>
    public sealed class FileChunker
    {
        private const long DefaultMaxFileSizeBytes = 1 * 1024 * 1024; // 1MB
        // Scan up to this many bytes of the file for null bytes. Many binary
        // formats (PDF, Adobe Illustrator, some archives) start with an ASCII
        // header and only reveal their binary payload later, so scanning only
        // the first few KB lets them slip through. 256KB is cheap to read and
        // catches the common offenders without hurting real text files.
        private const int BinaryDetectionBytes = 256 * 1024;

        private readonly string _repoRoot;
        private readonly IChunkerSelector _selector;
        private readonly long _maxFileSizeBytes;

        public FileChunker(string repoRoot, IChunkerSelector selector, long? maxFileSizeBytes = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
            ArgumentNullException.ThrowIfNull(selector);

            _repoRoot = repoRoot;
            _selector = selector;
            _maxFileSizeBytes = maxFileSizeBytes ?? DefaultMaxFileSizeBytes;
        }

        /// <summary>
        /// Prepares a single file for indexing. Returns null if the file does not
        /// exist on disk. Returns a result with an empty chunk list if the file
        /// exists but is filtered out (too large, binary). The fingerprint is
        /// always populated for existing files so the caller can update the
        /// store even when the file was skipped.
        /// </summary>
        public async Task<PreparedFile?> PrepareAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

            string absolutePath = Path.Combine(_repoRoot, relativePath);
            if (!File.Exists(absolutePath))
                return null;

            FileInfo info = new FileInfo(absolutePath);
            FileFingerprint fingerprint = FileFingerprint.FromFile(relativePath, info);

            // Check the chunker allowlist first — it's a fast dictionary lookup,
            // and if the extension is unknown we can skip the expensive binary
            // scan and chunking work entirely.
            ChunkerConfig? config = _selector.SelectFor(relativePath);
            if (config == null)
            {
                // File isn't chunkable, but if its extension is on the
                // filename-only allowlist (images, PDFs, design files) we
                // still emit a single synthetic chunk so the file is
                // searchable by name.
                if (_selector.ShouldIndexByFilename(relativePath))
                {
                    return new PreparedFile(fingerprint, new List<ChunkDraft>
                    {
                        new ChunkDraft
                        {
                            RelativePath = relativePath,
                            StartLine = 1,
                            EndLine = 1,
                            Text = $"[asset: {relativePath}]",
                            EmbeddingText = relativePath,
                        },
                    });
                }

                return new PreparedFile(fingerprint, Array.Empty<ChunkDraft>());
            }

            if (info.Length > _maxFileSizeBytes)
                return new PreparedFile(fingerprint, Array.Empty<ChunkDraft>());

            if (await IsBinaryAsync(absolutePath, cancellationToken))
                return new PreparedFile(fingerprint, Array.Empty<ChunkDraft>());

            List<ChunkDraft> drafts = new List<ChunkDraft>();

            await using FileStream fileStream = File.OpenRead(absolutePath);
            using StreamReader reader = new StreamReader(fileStream);

            ChunkReader chunker = ChunkReader.Create(reader, TokenCounter.EstimateTokens, new ChunkReaderOptions
            {
                MaxTokensPerChunk = config.MaxTokensPerChunk,
                BreakStrings = config.BreakStrings,
                StopSignals = config.StopSignals,
            });

            int currentLine = 1;
            await foreach (string chunkText in chunker.ReadAllAsync(cancellationToken))
            {
                if (string.IsNullOrEmpty(chunkText))
                    continue;

                int startLine = currentLine;
                int newlineCount = CountNewlines(chunkText);
                bool endsWithNewline = chunkText[^1] == '\n';
                int endLine = endsWithNewline ? startLine + newlineCount - 1 : startLine + newlineCount;

                // Defensive: ChunkReader preserves every input byte by contract; if that
                // ever breaks we'd silently drift. Snap any invalid range to a single line.
                if (endLine < startLine) endLine = startLine;

                drafts.Add(new ChunkDraft
                {
                    RelativePath = relativePath,
                    StartLine = startLine,
                    EndLine = endLine,
                    Text = chunkText,
                });

                currentLine = endsWithNewline ? endLine + 1 : endLine;
            }

            return new PreparedFile(fingerprint, drafts);
        }

        private static int CountNewlines(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') count++;
            }
            return count;
        }

        private static async Task<bool> IsBinaryAsync(string path, CancellationToken cancellationToken)
        {
            await using FileStream stream = File.OpenRead(path);
            byte[] buffer = new byte[BinaryDetectionBytes];
            int read = await stream.ReadAsync(buffer, cancellationToken);
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == 0) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A single file's prepared indexing state.
    /// </summary>
    public sealed record PreparedFile(FileFingerprint Fingerprint, IReadOnlyList<ChunkDraft> Chunks);
}
