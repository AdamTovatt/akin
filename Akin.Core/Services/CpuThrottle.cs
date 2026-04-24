using System.Diagnostics;

namespace Akin.Core.Services
{
    /// <summary>
    /// Measures this process's CPU usage over a sliding window and inserts
    /// delays to keep usage at or below a target percentage. Call
    /// <see cref="ThrottleAsync"/> between units of work (e.g. after each
    /// file is embedded).
    /// </summary>
    /// <remarks>
    /// Not thread-safe. Callers must ensure <see cref="ThrottleAsync"/> is called
    /// sequentially (the indexer loop satisfies this naturally).
    /// </remarks>
    internal sealed class CpuThrottle : IDisposable
    {
        private readonly int _targetPercent;
        private readonly int _processorCount;
        private readonly Process _process;
        private TimeSpan _lastCpuTime;
        private long _lastTimestamp;

        public CpuThrottle(int targetPercent)
        {
            _targetPercent = Math.Clamp(targetPercent, 1, 100);
            _processorCount = Environment.ProcessorCount;
            _process = Process.GetCurrentProcess();
            _lastCpuTime = _process.TotalProcessorTime;
            _lastTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// If the process CPU usage since the last call exceeds the target,
        /// delays long enough to bring the average back down. No-ops if
        /// usage is within budget.
        /// </summary>
        public async Task ThrottleAsync(CancellationToken cancellationToken)
        {
            if (_targetPercent >= 100)
                return;

            _process.Refresh();
            TimeSpan currentCpuTime = _process.TotalProcessorTime;
            long currentTimestamp = Stopwatch.GetTimestamp();

            double elapsedSeconds = Stopwatch.GetElapsedTime(_lastTimestamp, currentTimestamp).TotalSeconds;
            if (elapsedSeconds < 0.05)
                return; // Too short to measure meaningfully.

            double cpuSeconds = (currentCpuTime - _lastCpuTime).TotalSeconds;
            double cpuPercent = cpuSeconds / (elapsedSeconds * _processorCount) * 100.0;

            if (cpuPercent > _targetPercent)
            {
                // How long to sleep so that (cpuSeconds / (elapsed + sleep) / cores) = target%
                // Solving: sleep = (cpuSeconds / (target/100 * cores)) - elapsed
                double desiredTotal = cpuSeconds / (_targetPercent / 100.0 * _processorCount);
                double sleepSeconds = desiredTotal - elapsedSeconds;
                int sleepMs = Math.Clamp((int)(sleepSeconds * 1000), 10, 5000);

                await Task.Delay(sleepMs, cancellationToken);

                // Reset the window after sleeping so the next measurement starts
                // from a clean baseline. Without this, the sleep time would be
                // counted as idle in the next window, under-throttling.
                _process.Refresh();
                _lastCpuTime = _process.TotalProcessorTime;
                _lastTimestamp = Stopwatch.GetTimestamp();
            }
            // When under budget, do NOT reset — let the measurement window
            // accumulate so a burst of heavy work across multiple calls is
            // still detected.
        }

        public void Dispose()
        {
            _process.Dispose();
        }
    }
}
