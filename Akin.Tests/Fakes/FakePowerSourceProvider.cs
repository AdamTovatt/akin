using Akin.Core.Interfaces;

namespace Akin.Tests.Fakes
{
    /// <summary>
    /// Drives <see cref="IPowerSourceProvider"/> from a mutable bool so tests
    /// can simulate AC ↔ battery transitions without touching the real
    /// platform APIs. Also tracks the call count so tests can assert how
    /// often the pause-gated loops re-checked the power state.
    /// </summary>
    internal sealed class FakePowerSourceProvider : IPowerSourceProvider
    {
        public bool OnBattery { get; set; }
        public int CallCount { get; private set; }

        public Task<bool> IsOnBatteryAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(OnBattery);
        }
    }
}
