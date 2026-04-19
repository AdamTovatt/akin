using System.Collections.Concurrent;

namespace Akin.Core.Services
{
    /// <summary>
    /// Keeps the on-disk index in sync with the working tree for the duration of
    /// a long-running process (typically the MCP server). Watches the repository
    /// for file changes, debounces them, and triggers incremental reindexes.
    /// Runs a periodic full reconciliation against <c>git ls-files</c> to catch
    /// changes that the watcher dropped or that involve gitignore reclassification.
    /// </summary>
    public sealed class IndexCoordinator : IAsyncDisposable
    {
        private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DefaultReconciliationInterval = TimeSpan.FromMinutes(5);

        private readonly RepoContext _context;
        private readonly TimeSpan _flushInterval;
        private readonly TimeSpan _reconciliationInterval;
        private readonly Action<string, Exception> _reportError;

        private readonly FileSystemWatcher _watcher;
        private readonly ConcurrentDictionary<string, byte> _pending = new ConcurrentDictionary<string, byte>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _processLock = new SemaphoreSlim(1, 1);

        // _trackedFiles is swapped atomically by reference; readers use Volatile.Read
        // to avoid stale views on weakly-ordered architectures.
        private HashSet<string> _trackedFiles = new HashSet<string>(StringComparer.Ordinal);
        private Task? _flushTask;
        private Task? _reconciliationTask;
        private bool _disposed;

        private IndexCoordinator(
            RepoContext context,
            TimeSpan flushInterval,
            TimeSpan reconciliationInterval,
            Action<string, Exception> reportError)
        {
            _context = context;
            _flushInterval = flushInterval;
            _reconciliationInterval = reconciliationInterval;
            _reportError = reportError;

            _watcher = new FileSystemWatcher(context.RepoRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                InternalBufferSize = 64 * 1024,
            };
        }

        public static async Task<IndexCoordinator> StartAsync(RepoContext context, CancellationToken cancellationToken = default)
        {
            return await StartAsync(context, DefaultFlushInterval, DefaultReconciliationInterval, reportError: null, cancellationToken);
        }

        public static async Task<IndexCoordinator> StartAsync(
            RepoContext context,
            TimeSpan flushInterval,
            TimeSpan reconciliationInterval,
            Action<string, Exception>? reportError = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            Action<string, Exception> effectiveReport = reportError ?? DefaultReport;

            IndexCoordinator coordinator = new IndexCoordinator(context, flushInterval, reconciliationInterval, effectiveReport);
            await coordinator.RefreshTrackedSetAsync(cancellationToken);
            coordinator.AttachHandlers();
            coordinator._watcher.EnableRaisingEvents = true;

            coordinator._flushTask = Task.Run(() => coordinator.FlushLoopAsync(coordinator._cts.Token));
            coordinator._reconciliationTask = Task.Run(() => coordinator.ReconciliationLoopAsync(coordinator._cts.Token));

            return coordinator;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _watcher.EnableRaisingEvents = false;
            await _cts.CancelAsync();

            try
            {
                if (_flushTask != null) await _flushTask;
                if (_reconciliationTask != null) await _reconciliationTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }

            _watcher.Dispose();
            _cts.Dispose();
            _processLock.Dispose();
        }

        private static void DefaultReport(string phase, Exception ex)
        {
            Console.Error.WriteLine($"[akin coordinator] {phase}: {ex.GetType().Name}: {ex.Message}");
        }

        private void AttachHandlers()
        {
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnRenamed;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string? relativePath = NormalizeToRelative(e.FullPath);
            if (relativePath == null) return;
            _pending.TryAdd(relativePath, 0);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            string? oldRel = NormalizeToRelative(e.OldFullPath);
            if (oldRel != null) _pending.TryAdd(oldRel, 0);

            string? newRel = NormalizeToRelative(e.FullPath);
            if (newRel != null) _pending.TryAdd(newRel, 0);
        }

        private string? NormalizeToRelative(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return null;

            // macOS APFS and Windows NTFS are case-insensitive by default, so the
            // watcher's FullPath may use a different case than our stored repo root.
            // Fall back to case-insensitive matching on those platforms.
            StringComparison pathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            string rootWithSep = _context.RepoRoot.EndsWith(Path.DirectorySeparatorChar)
                ? _context.RepoRoot
                : _context.RepoRoot + Path.DirectorySeparatorChar;

            if (!absolutePath.StartsWith(rootWithSep, pathComparison))
                return null;

            string relative = absolutePath.Substring(rootWithSep.Length).Replace('\\', '/');
            if (relative.Length == 0) return null;

            if (relative.StartsWith(".git/", StringComparison.Ordinal) || relative == ".git") return null;
            if (relative.StartsWith(".akin/", StringComparison.Ordinal) || relative == ".akin") return null;

            return relative;
        }

        private async Task FlushLoopAsync(CancellationToken cancellationToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(_flushInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    string[] batch = _pending.Keys.ToArray();
                    foreach (string key in batch)
                    {
                        _pending.TryRemove(key, out _);
                    }

                    if (batch.Length == 0) continue;

                    await _processLock.WaitAsync(cancellationToken);
                    try
                    {
                        foreach (string path in batch)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                await ProcessPathAsync(path, cancellationToken);
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _reportError($"flush ({path})", ex);
                            }
                        }
                    }
                    finally
                    {
                        _processLock.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        private async Task ReconciliationLoopAsync(CancellationToken cancellationToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(_reconciliationInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await _processLock.WaitAsync(cancellationToken);
                    try
                    {
                        await ReconcileLockedAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _reportError("reconciliation", ex);
                    }
                    finally
                    {
                        _processLock.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        private async Task RefreshTrackedSetAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<string> scanned = await _context.Scanner.ScanAsync(cancellationToken);
            Volatile.Write(ref _trackedFiles, new HashSet<string>(scanned, StringComparer.Ordinal));
        }

        private async Task ProcessPathAsync(string relativePath, CancellationToken cancellationToken)
        {
            HashSet<string> trackedSnapshot = Volatile.Read(ref _trackedFiles);
            if (trackedSnapshot.Contains(relativePath))
            {
                // File is tracked — let the indexer decide if there is anything to embed.
                // ReindexFileAsync handles both "file present → embed" and "file missing → remove".
                await _context.Indexer.ReindexFileAsync(relativePath, cancellationToken);
            }
            else
            {
                // Not in the tracked set. If we have chunks for it, drop them. This also
                // covers the brief window between a file being created and the next
                // reconciliation sweep picking it up.
                await _context.Store.RemoveFileAsync(relativePath, cancellationToken);
            }
        }

        private async Task ReconcileLockedAsync(CancellationToken cancellationToken)
        {
            // Delegate to the shared reconciler which handles new files, deleted files,
            // and files whose fingerprint has changed — catching anything the watcher missed.
            await _context.Reconciler.ReconcileAsync(cancellationToken);
            await RefreshTrackedSetAsync(cancellationToken);
        }
    }
}
