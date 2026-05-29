using Akin.Core.Models;

namespace Akin.Core.Interfaces
{
    /// <summary>
    /// Provides the effective indexing pause flag — combining the manual
    /// pause switch, the auto-pause-on-battery setting, and the current
    /// power source — to the long-running indexing loops, cheaply enough to
    /// be polled on every tick. Abstracted so the loops can be unit-tested
    /// without touching the real user-level state file or native power APIs.
    /// </summary>
    public interface IGlobalStateWatcher
    {
        /// <summary>
        /// Returns whether background indexing should currently be paused.
        /// Equivalent to <c>(await GetPauseStateAsync()).EffectivePaused</c>;
        /// kept as a dedicated method so the hot indexing-loop callsites
        /// read as a single bool rather than reaching into a struct.
        /// </summary>
        Task<bool> IsPausedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the full snapshot of inputs that decide the effective
        /// pause state. Used by status reporting to explain *why* indexing is
        /// paused without re-reading the state file or polling the power
        /// source separately.
        /// </summary>
        Task<PauseState> GetPauseStateAsync(CancellationToken cancellationToken = default);
    }
}
