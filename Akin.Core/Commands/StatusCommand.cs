using System.Text;
using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Reports the current state of the index: whether it is ready, how many
    /// files and chunks are stored, and whether its manifest still matches
    /// the current embedding and chunker configuration.
    /// </summary>
    public sealed class StatusCommand : ICommand
    {
        private readonly IIndexStore _store;
        private readonly string _repoRoot;
        private readonly string _indexFolder;
        private readonly bool _compatible;
        private readonly AkinConfig _config;
        private readonly IGlobalStateWatcher _globalState;

        public StatusCommand(IIndexStore store, string repoRoot, string indexFolder, bool compatible, AkinConfig config, IGlobalStateWatcher globalState)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(indexFolder);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(globalState);

            _store = store;
            _repoRoot = repoRoot;
            _indexFolder = indexFolder;
            _compatible = compatible;
            _config = config;
            _globalState = globalState;
        }

        public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            IndexStatus status = _store.GetStatus();
            PauseState pauseState = await _globalState.GetPauseStateAsync(cancellationToken);

            StringBuilder details = new StringBuilder();
            details.Append("Repo root:        ").AppendLine(_repoRoot);
            details.Append("Index folder:     ").AppendLine(_indexFolder);
            details.Append("Ready:            ").AppendLine(status.IsReady ? "yes" : "no (run `akin reindex`)");
            details.Append("Files:            ").AppendLine(status.FileCount.ToString());
            details.Append("Chunks:           ").AppendLine(status.ChunkCount.ToString());
            details.Append("Compatible:       ").AppendLine(_compatible ? "yes" : "no (rebuild recommended)");
            details.Append("Indexing:         ").AppendLine(FormatIndexingLine(pauseState));
            details.Append("Auto-pause:       ").AppendLine(FormatAutoPauseLine(pauseState));

            if (status.Manifest != null)
            {
                details.Append("Model:            ").AppendLine(status.Manifest.EmbeddingModel);
                details.Append("Dimension:        ").AppendLine(status.Manifest.EmbeddingDimension.ToString());
                details.Append("Last updated UTC: ").AppendLine(status.Manifest.LastIndexUpdateUtc.ToString("u"));
            }

            details.Append("Max CPU:          ").Append(_config.MaxCpuPercent.ToString()).AppendLine("%");
            details.Append("ONNX threads:     ").AppendLine(_config.DerivedIntraOpNumThreads.ToString());

            string message = status.IsReady ? "Index ready." : "Index not built yet.";
            return new CommandResult(true, message, details.ToString().TrimEnd());
        }

        private static string FormatIndexingLine(PauseState state)
        {
            if (state.ManuallyPaused)
                return "paused (manual — run `akin resume`)";

            if (state.AutoPauseEnabled && state.OnBattery)
                return "paused (on battery — plug in to resume, or run `akin auto-pause off`)";

            return "active";
        }

        private static string FormatAutoPauseLine(PauseState state)
        {
            string mode = state.AutoPauseEnabled ? "on" : "off";
            string powerSource = state.OnBattery ? "battery" : "AC power";
            return $"{mode} (currently on {powerSource})";
        }
    }
}
