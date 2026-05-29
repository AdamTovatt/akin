namespace Akin.Core.Models
{
    /// <summary>
    /// Snapshot of every input that decides whether background indexing
    /// should currently run. Lets the status command explain *why* indexing
    /// is paused (manual switch, battery, or both) without re-reading the
    /// global state and the power source separately. <see cref="EffectivePaused"/>
    /// is the single bool the indexing loops actually act on.
    /// </summary>
    public readonly record struct PauseState
    {
        public PauseState(bool manuallyPaused, bool autoPauseEnabled, bool onBattery)
        {
            ManuallyPaused = manuallyPaused;
            AutoPauseEnabled = autoPauseEnabled;
            OnBattery = onBattery;
        }

        /// <summary>
        /// The user explicitly ran <c>akin pause</c> and has not yet run
        /// <c>akin resume</c>. Always wins over auto-pause.
        /// </summary>
        public bool ManuallyPaused { get; }

        /// <summary>
        /// Whether the user has opted into the auto-pause-on-battery
        /// behaviour. On by default; <c>akin auto-pause off</c> turns it off.
        /// </summary>
        public bool AutoPauseEnabled { get; }

        /// <summary>
        /// Whether the power source provider currently reports the machine is
        /// drawing from a battery. Always <c>false</c> on platforms where we
        /// have no detection wired up.
        /// </summary>
        public bool OnBattery { get; }

        /// <summary>
        /// The single bool the indexing loops act on: paused if the user
        /// manually paused, or if auto-pause is on and we are on battery.
        /// </summary>
        public bool EffectivePaused => ManuallyPaused || (AutoPauseEnabled && OnBattery);
    }
}
