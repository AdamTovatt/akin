using Akin.Core.Services;
using Microsoft.Extensions.Hosting;

namespace Akin.Cli
{
    /// <summary>
    /// Hosts an <see cref="IndexCoordinator"/> and an initial ensure-ready pass
    /// for the lifetime of the MCP server. <see cref="StartAsync"/> returns
    /// immediately — the coordinator is started and the initial reindex runs in
    /// a detached task — so the MCP stdio handshake is not blocked by index
    /// construction. Searches arriving before the index is ready just get
    /// empty results until the background pass completes.
    ///
    /// Only one instance per repository holds the indexer lock at a time.
    /// Additional instances serve searches from the on-disk index but do not
    /// run a coordinator or trigger reindexes. If the lock holder exits, one
    /// of the remaining instances promotes itself to indexer.
    /// </summary>
    internal sealed class IndexBackgroundService : IHostedService, IAsyncDisposable
    {
        private static readonly TimeSpan LockRetryInterval = TimeSpan.FromSeconds(30);
        private const int MaxBootstrapRetries = 3;

        private readonly RepoContext _context;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private IndexCoordinator? _coordinator;
        private FileStream? _lockStream;
        private Task? _backgroundTask;
        private int _disposed;

        public IndexBackgroundService(RepoContext context)
        {
            _context = context;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _backgroundTask = Task.Run(() => RunAsync(_cts.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _cts.CancelAsync();

            if (_backgroundTask != null)
            {
                try { await _backgroundTask; }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[akin] background task ended with error: {ex.Message}");
                }
            }

            await DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            if (_coordinator != null)
            {
                await _coordinator.DisposeAsync();
                _coordinator = null;
            }

            if (_lockStream != null)
            {
                await _lockStream.DisposeAsync();
                _lockStream = null;
            }

            _cts.Dispose();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            bool loggedContention = false;
            int bootstrapFailures = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (TryAcquireLock())
                {
                    try
                    {
                        await BootstrapAsync(cancellationToken);
                        return; // Coordinator is running; stay as indexer until shutdown.
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        bootstrapFailures++;
                        ReleaseLock();

                        if (bootstrapFailures >= MaxBootstrapRetries)
                        {
                            Console.Error.WriteLine($"[akin] bootstrap failed {bootstrapFailures} times, giving up: {ex.Message}");
                            return;
                        }

                        Console.Error.WriteLine($"[akin] bootstrap failed (attempt {bootstrapFailures}/{MaxBootstrapRetries}), releasing lock: {ex.Message}");
                    }
                }
                else if (!loggedContention)
                {
                    Console.Error.WriteLine("[akin] another instance is indexing this repo, waiting...");
                    loggedContention = true;
                }

                try
                {
                    await Task.Delay(LockRetryInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void ReleaseLock()
        {
            if (_lockStream != null)
            {
                _lockStream.Dispose();
                _lockStream = null;
            }
        }

        private bool TryAcquireLock()
        {
            if (_lockStream != null) return true;

            try
            {
                Directory.CreateDirectory(_context.IndexFolder);

                string lockPath = Path.Combine(_context.IndexFolder, ".lock");
                _lockStream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                return true;
            }
            catch (IOException)
            {
                // Another process holds the lock.
                return false;
            }
        }

        private async Task BootstrapAsync(CancellationToken cancellationToken)
        {
            // Reload the index from disk in case a previous lock holder updated it.
            await _context.Store.OpenAsync(cancellationToken);

            // Start the file watcher first so any changes during the initial
            // reindex pass are captured and replayed after.
            _coordinator = await IndexCoordinator.StartAsync(_context, cancellationToken);

            try
            {
                await _context.EnsureIndexReadyAsync(progress: null, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[akin] initial index build failed: {ex.Message}");
            }
        }
    }
}
