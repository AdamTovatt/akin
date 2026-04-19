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
    /// </summary>
    internal sealed class IndexBackgroundService : IHostedService, IAsyncDisposable
    {
        private readonly RepoContext _context;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private IndexCoordinator? _coordinator;
        private Task? _bootstrapTask;

        public IndexBackgroundService(RepoContext context)
        {
            _context = context;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _bootstrapTask = Task.Run(() => BootstrapAsync(_cts.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _cts.CancelAsync();

            if (_bootstrapTask != null)
            {
                try { await _bootstrapTask; }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[akin] bootstrap task ended with error: {ex.Message}");
                }
            }

            await DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_coordinator != null)
            {
                await _coordinator.DisposeAsync();
                _coordinator = null;
            }
            _cts.Dispose();
        }

        private async Task BootstrapAsync(CancellationToken cancellationToken)
        {
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
