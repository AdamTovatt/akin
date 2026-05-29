using Akin.Core.Interfaces;

namespace Akin.Core.Services
{
    /// <summary>
    /// Fallback <see cref="IPowerSourceProvider"/> for platforms where we have
    /// no battery-state detection wired up yet (currently everything except
    /// macOS). Always reports "on AC power" so auto-pause never engages and a
    /// desktop machine never has indexing silently disabled because of a
    /// platform we did not specifically implement.
    /// </summary>
    internal sealed class UnknownPowerSourceProvider : IPowerSourceProvider
    {
        public Task<bool> IsOnBatteryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
