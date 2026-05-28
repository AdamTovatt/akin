using Akin.Core.Interfaces;

namespace Akin.Tests.Fakes
{
    /// <summary>
    /// Returns a fixed pause flag and counts how many times it was polled, so
    /// tests can drive and observe the pause-gated read path without touching
    /// the real user-level state file.
    /// </summary>
    internal sealed class StubGlobalStateWatcher : IGlobalStateWatcher
    {
        private readonly bool _paused;

        public StubGlobalStateWatcher(bool paused)
        {
            _paused = paused;
        }

        public int PollCount { get; private set; }

        public Task<bool> IsPausedAsync(CancellationToken cancellationToken = default)
        {
            PollCount++;
            return Task.FromResult(_paused);
        }
    }
}
