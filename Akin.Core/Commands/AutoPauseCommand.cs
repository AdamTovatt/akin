using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Toggles the global auto-pause-on-battery flag. When on (the default),
    /// every running <c>akin</c> MCP server suspends background indexing
    /// while the machine is on battery and resumes when plugged back in;
    /// when off, indexing stays active regardless of power source and only
    /// the manual <c>akin pause</c> can stop it. The change is persisted to
    /// <c>~/.akin/state.json</c>; running servers notice within a few
    /// seconds of their next pause-state poll.
    /// </summary>
    public sealed class AutoPauseCommand : ICommand
    {
        private readonly string _stateFolder;
        private readonly bool _enable;

        public AutoPauseCommand(string stateFolder, bool enable)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateFolder);
            _stateFolder = stateFolder;
            _enable = enable;
        }

        public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            GlobalState current = await GlobalState.LoadAsync(_stateFolder, cancellationToken);

            if (current.AutoPauseOnBattery == _enable)
            {
                return new CommandResult(true, _enable
                    ? "Auto-pause already on."
                    : "Auto-pause already off.");
            }

            GlobalState updated = current with { AutoPauseOnBattery = _enable };
            await updated.SaveAsync(_stateFolder, cancellationToken);

            return new CommandResult(true, _enable
                ? "Auto-pause on. Background indexing will pause while on battery and resume when plugged in. Detection currently only works on macOS — other platforms behave as if always plugged in."
                : "Auto-pause off. Background indexing stays active regardless of power source. Use 'akin pause' to stop it manually.");
        }
    }
}
