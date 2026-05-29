using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Tests.Fakes
{
    /// <summary>
    /// Returns a fixed effective pause state and counts how many times it was
    /// polled, so tests can drive and observe the pause-gated read path
    /// without touching the real user-level state file or platform power
    /// APIs. The constructor that takes a single bool keeps the most common
    /// "effectively paused yes/no" test setup compact; tests that care about
    /// the manual/auto/battery breakdown use the full constructor.
    /// </summary>
    internal sealed class StubGlobalStateWatcher : IGlobalStateWatcher
    {
        private readonly PauseState _state;

        public StubGlobalStateWatcher(bool paused)
            : this(new PauseState(manuallyPaused: paused, autoPauseEnabled: false, onBattery: false))
        {
        }

        public StubGlobalStateWatcher(PauseState state)
        {
            _state = state;
        }

        public int PollCount { get; private set; }

        public Task<bool> IsPausedAsync(CancellationToken cancellationToken = default)
        {
            PollCount++;
            return Task.FromResult(_state.EffectivePaused);
        }

        public Task<PauseState> GetPauseStateAsync(CancellationToken cancellationToken = default)
        {
            PollCount++;
            return Task.FromResult(_state);
        }
    }
}
