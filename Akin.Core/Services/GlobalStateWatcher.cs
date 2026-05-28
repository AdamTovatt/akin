using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Services
{
    /// <summary>
    /// Caches the most recently observed <see cref="GlobalState"/> for a short
    /// window so the indexing loops can cheaply check whether they should pause
    /// without re-reading <c>state.json</c> on every tick.
    /// </summary>
    public sealed class GlobalStateWatcher : IGlobalStateWatcher
    {
        /// <summary>
        /// How long a read of the global state is cached before the next access
        /// re-reads from disk. Also the recommended poll cadence for callers
        /// that wait on the pause flag, so an unpause is observed within roughly
        /// this interval everywhere.
        /// </summary>
        public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

        private readonly string _stateFolder;
        private readonly TimeSpan _refreshInterval;
        private readonly object _gate = new object();

        private GlobalState _cached = new GlobalState();
        private DateTime _cachedAtUtc = DateTime.MinValue;
        private Task<GlobalState>? _inFlight;

        public GlobalStateWatcher()
            : this(GlobalState.DefaultStateFolder, PollInterval)
        {
        }

        public GlobalStateWatcher(string stateFolder, TimeSpan? refreshInterval = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateFolder);

            _stateFolder = stateFolder;
            _refreshInterval = refreshInterval ?? PollInterval;
        }

        /// <summary>
        /// Returns whether background indexing should currently be paused.
        /// Reads from a short-lived cache; refreshes from disk when stale.
        /// </summary>
        public async Task<bool> IsPausedAsync(CancellationToken cancellationToken = default)
        {
            GlobalState state = await GetStateAsync(cancellationToken);
            return state.IndexingPaused;
        }

        private Task<GlobalState> GetStateAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (DateTime.UtcNow - _cachedAtUtc < _refreshInterval)
                    return Task.FromResult(_cached);

                _inFlight ??= RefreshAsync(cancellationToken);
                return _inFlight;
            }
        }

        private async Task<GlobalState> RefreshAsync(CancellationToken cancellationToken)
        {
            try
            {
                GlobalState fresh = await GlobalState.LoadAsync(_stateFolder, cancellationToken);
                lock (_gate)
                {
                    _cached = fresh;
                    _cachedAtUtc = DateTime.UtcNow;
                    _inFlight = null;
                }
                return fresh;
            }
            catch
            {
                lock (_gate) { _inFlight = null; }
                throw;
            }
        }
    }
}
