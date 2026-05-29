namespace Akin.Core.Interfaces
{
    /// <summary>
    /// Reports whether the machine is currently running on battery power, so
    /// the indexing loops can suspend embedding work to avoid draining the
    /// battery. Implementations are expected to be cheap and exception-free:
    /// any platform we cannot interrogate (Linux, Windows, or a failed macOS
    /// IOKit call) reports <c>false</c> so auto-pause behaves as a silent
    /// no-op rather than accidentally pausing a desktop. Abstracted so the
    /// pause logic can be unit-tested without touching native APIs.
    /// </summary>
    public interface IPowerSourceProvider
    {
        /// <summary>
        /// Returns <c>true</c> only when we have positively determined that the
        /// machine is drawing from a battery. Unknown / unsupported platforms
        /// return <c>false</c>.
        /// </summary>
        Task<bool> IsOnBatteryAsync(CancellationToken cancellationToken = default);
    }
}
