using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Services
{
    /// <summary>
    /// Brings the index up to date by comparing the working tree's current files
    /// against the stored per-file fingerprints. Files whose size or modification
    /// time has changed since the last indexing pass are reindexed; files that
    /// have disappeared are removed. Writes are batched so a reconciliation with
    /// many deltas persists once at the end instead of after every file.
    /// </summary>
    public sealed class IndexReconciler
    {
        private readonly string _repoRoot;
        private readonly IRepoScanner _scanner;
        private readonly IIndexStore _store;
        private readonly IIndexer _indexer;

        public IndexReconciler(string repoRoot, IRepoScanner scanner, IIndexStore store, IIndexer indexer)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
            ArgumentNullException.ThrowIfNull(scanner);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(indexer);

            _repoRoot = repoRoot;
            _scanner = scanner;
            _store = store;
            _indexer = indexer;
        }

        public async Task<ReconciliationResult> ReconcileAsync(IProgress<IndexProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new IndexProgress { Phase = "scanning" });

            IReadOnlyList<string> tracked = await _scanner.ScanAsync(cancellationToken);
            HashSet<string> trackedSet = new HashSet<string>(tracked, StringComparer.Ordinal);

            int reindexed = 0;
            int removed = 0;

            await using IAsyncDisposable batch = await _store.BeginBatchAsync(cancellationToken);

            // Pass 1: drop files that are no longer tracked.
            foreach (FileFingerprint existing in _store.AllFingerprints().ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!trackedSet.Contains(existing.RelativePath))
                {
                    await _store.RemoveFileAsync(existing.RelativePath, cancellationToken);
                    removed++;
                }
            }

            // First pass to count how much actual work is pending so we can give
            // the caller a denominator before we start reindexing.
            List<string> toReindex = new List<string>();
            foreach (string relativePath in tracked)
            {
                FileFingerprint? stored = _store.GetFingerprint(relativePath);
                string absolutePath = Path.Combine(_repoRoot, relativePath);
                if (!File.Exists(absolutePath))
                {
                    if (stored != null)
                    {
                        await _store.RemoveFileAsync(relativePath, cancellationToken);
                        removed++;
                    }
                    continue;
                }

                FileInfo info = new FileInfo(absolutePath);
                if (stored != null && stored.MatchesCurrentFile(info)) continue;

                toReindex.Add(relativePath);
            }

            progress?.Report(new IndexProgress
            {
                Phase = "indexing",
                FilesDone = 0,
                FilesTotal = toReindex.Count,
            });

            for (int i = 0; i < toReindex.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new IndexProgress
                {
                    Phase = "indexing",
                    CurrentFile = toReindex[i],
                    FilesDone = i,
                    FilesTotal = toReindex.Count,
                });

                await _indexer.ReindexFileAsync(toReindex[i], cancellationToken);
                reindexed++;
            }

            return new ReconciliationResult(reindexed, removed, tracked.Count);
        }
    }

    /// <summary>
    /// Summary of a reconciliation pass.
    /// </summary>
    public sealed record ReconciliationResult(int FilesReindexed, int FilesRemoved, int TrackedFiles);
}
