using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Services
{
    /// <summary>
    /// Caches the most recently observed <see cref="GlobalState"/> and
    /// <see cref="IPowerSourceProvider"/> result for a short window so the
    /// indexing loops can cheaply check whether they should pause without
    /// re-reading <c>state.json</c> or invoking IOKit on every tick.
    /// </summary>
    public sealed class GlobalStateWatcher : IGlobalStateWatcher
    {
        /// <summary>
        /// How long a read of the global state is cached before the next
        /// access re-reads from disk and re-polls the power source. Also the
        /// recommended poll cadence for callers that wait on the pause flag,
        /// so a transition (manual unpause, plug/unplug) is observed within
        /// roughly this interval everywhere.
        /// </summary>
        public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

        private readonly string _stateFolder;
        private readonly IPowerSourceProvider _powerSource;
        private readonly TimeSpan _refreshInterval;
        private readonly object _gate = new object();

        private PauseState _cached = default;
        private DateTime _cachedAtUtc = DateTime.MinValue;

        public GlobalStateWatcher(string stateFolder, IPowerSourceProvider powerSource, TimeSpan? refreshInterval = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateFolder);
            ArgumentNullException.ThrowIfNull(powerSource);

            _stateFolder = stateFolder;
            _powerSource = powerSource;
            _refreshInterval = refreshInterval ?? PollInterval;
        }

        public async Task<bool> IsPausedAsync(CancellationToken cancellationToken = default)
        {
            PauseState state = await GetPauseStateAsync(cancellationToken);
            return state.EffectivePaused;
        }

        public async Task<PauseState> GetPauseStateAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (DateTime.UtcNow - _cachedAtUtc < _refreshInterval)
                    return _cached;
            }

            // Refresh both inputs together so the snapshot we cache is
            // internally consistent — e.g. status reports never show
            // "auto-pause on, on AC, paused (battery)" because of two
            // independently aged reads.
            GlobalState freshState = await GlobalState.LoadAsync(_stateFolder, cancellationToken);
            bool onBattery = await _powerSource.IsOnBatteryAsync(cancellationToken);

            PauseState fresh = new PauseState(
                manuallyPaused: freshState.IndexingPaused,
                autoPauseEnabled: freshState.AutoPauseOnBattery,
                onBattery: onBattery);

            lock (_gate)
            {
                _cached = fresh;
                _cachedAtUtc = DateTime.UtcNow;
            }

            return fresh;
        }
    }
}
