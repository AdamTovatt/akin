using System.Text;
using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Views or updates settings in <c>.akin/config.json</c>.
    /// With no arguments, prints the current configuration.
    /// With <c>--set key value</c>, updates a single setting.
    /// </summary>
    public sealed class ConfigCommand : ICommand
    {
        private readonly string _indexFolder;
        private readonly string? _key;
        private readonly string? _value;

        public ConfigCommand(string indexFolder, string? key = null, string? value = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexFolder);

            _indexFolder = indexFolder;
            _key = key;
            _value = value;
        }

        public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            AkinConfig config = await AkinConfig.LoadAsync(_indexFolder, cancellationToken);

            if (_key == null)
            {
                StringBuilder details = new StringBuilder();
                details.Append("maxCpuPercent:     ").AppendLine(config.MaxCpuPercent.ToString());
                details.Append("  → ONNX threads:  ").AppendLine(config.DerivedIntraOpNumThreads.ToString());
                details.AppendLine();
                details.AppendLine("Set a value with: akin config --set <key> <value>");
                return new CommandResult(true, "Current configuration:", details.ToString().TrimEnd());
            }

            if (_key.Equals("maxCpuPercent", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(_value, out int percent) || percent < 1 || percent > 100)
                    return new CommandResult(false, "maxCpuPercent must be an integer between 1 and 100.");
                config = config with { MaxCpuPercent = percent };
            }
            else
            {
                return new CommandResult(false, $"Unknown config key '{_key}'. Known keys: maxCpuPercent");
            }

            await config.SaveAsync(_indexFolder, cancellationToken);
            return new CommandResult(true, $"Set {_key} = {_value}. Restart akin for the change to take effect.");
        }
    }
}
