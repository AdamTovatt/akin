namespace Akin.Core.Interfaces
{
    /// <summary>
    /// Provides the indexing pause flag from the machine-wide global state to
    /// the long-running indexing loops, cheaply enough to be polled on every
    /// tick. Abstracted so the loops can be unit-tested without touching the
    /// real user-level state file.
    /// </summary>
    public interface IGlobalStateWatcher
    {
        /// <summary>
        /// Returns whether background indexing is currently paused.
        /// </summary>
        Task<bool> IsPausedAsync(CancellationToken cancellationToken = default);
    }
}
