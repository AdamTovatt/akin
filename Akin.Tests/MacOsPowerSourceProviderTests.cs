using Akin.Core.Services;

namespace Akin.Tests
{
    /// <summary>
    /// Smoke tests for the IOKit-backed power source provider. Effectively
    /// no-ops on non-macOS hosts — there is no way to verify correctness
    /// against a known battery state from inside the test, so the goal here
    /// is to confirm the P/Invoke surface is well-formed (right framework
    /// path, right signature, right CoreFoundation memory rules) and the
    /// call returns a bool without throwing. Correctness verification is
    /// manual: run on AC and on battery and eyeball the printed result.
    /// </summary>
    public class MacOsPowerSourceProviderTests
    {
        [Fact]
        public async Task IsOnBatteryAsync_OnMacOs_ReturnsWithoutThrowing()
        {
            if (!OperatingSystem.IsMacOS()) return;

            MacOsPowerSourceProvider provider = new MacOsPowerSourceProvider();

            bool onBattery = await provider.IsOnBatteryAsync();

            // Print the result so a developer running the test locally can
            // sanity-check it against their current power state. xUnit
            // captures Console.WriteLine into the test output.
            Console.WriteLine($"IOKit reports on-battery = {onBattery}");
        }

        [Fact]
        public async Task IsOnBatteryAsync_OnMacOs_IsStableAcrossRepeatedCalls()
        {
            if (!OperatingSystem.IsMacOS()) return;

            MacOsPowerSourceProvider provider = new MacOsPowerSourceProvider();

            // Hammer the provider to catch any CoreFoundation leak or
            // use-after-free that the memory rules in ReadProvidingSourceType
            // get wrong. If we mis-managed CFRelease the process would either
            // crash or steadily grow; running 100 iterations is cheap and
            // surfaces the obvious mistakes.
            bool first = await provider.IsOnBatteryAsync();
            for (int i = 0; i < 100; i++)
            {
                bool current = await provider.IsOnBatteryAsync();
                Assert.Equal(first, current);
            }
        }

        [Fact]
        public async Task IsOnBatteryAsync_OnMacOs_MatchesExpectedPowerState()
        {
            // Opt-in ground-truth check. Without the env var the test is a
            // no-op, so regular CI runs don't depend on the host's power
            // state. To verify manually:
            //   AKIN_EXPECTED_POWER=ac      dotnet test --filter MatchesExpectedPowerState
            //   AKIN_EXPECTED_POWER=battery dotnet test --filter MatchesExpectedPowerState
            string? expected = Environment.GetEnvironmentVariable("AKIN_EXPECTED_POWER")?.ToLowerInvariant();
            if (expected is null) return;
            if (!OperatingSystem.IsMacOS()) return;

            bool expectedOnBattery = expected switch
            {
                "battery" or "batt" => true,
                "ac" or "plugged" => false,
                _ => throw new InvalidOperationException(
                    $"AKIN_EXPECTED_POWER must be 'ac' or 'battery' (got '{expected}')."),
            };

            MacOsPowerSourceProvider provider = new MacOsPowerSourceProvider();
            bool actual = await provider.IsOnBatteryAsync();

            Assert.Equal(expectedOnBattery, actual);
        }
    }
}
