using Akin.Core.Services;
using Microsoft.Extensions.Hosting;

namespace Akin.Cli
{
    /// <summary>
    /// Hosts an <see cref="IndexCoordinator"/> for the lifetime of the MCP server,
    /// so incremental reindexing happens in the background while the server handles
    /// search requests.
    /// </summary>
    internal sealed class IndexBackgroundService : IHostedService, IAsyncDisposable
    {
        private readonly RepoContext _context;
        private IndexCoordinator? _coordinator;

        public IndexBackgroundService(RepoContext context)
        {
            _context = context;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _coordinator = await IndexCoordinator.StartAsync(_context, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return DisposeAsync().AsTask();
        }

        public async ValueTask DisposeAsync()
        {
            if (_coordinator != null)
            {
                await _coordinator.DisposeAsync();
                _coordinator = null;
            }
        }
    }
}
