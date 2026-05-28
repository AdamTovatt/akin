using System.Text.Json;
using System.Text.Json.Serialization;

namespace Akin.Core.Models
{
    /// <summary>
    /// Machine-wide runtime switch shared across every <c>akin</c> process on
    /// the current user account. Stored in the user's application data folder
    /// (e.g. <c>~/.akin/state.json</c> on Linux/macOS) so the CLI can flip a
    /// flag that all long-running MCP servers observe on their next poll.
    ///
    /// Today this just controls a global pause for background indexing. The
    /// MCP servers continue to serve searches from whatever is already on
    /// disk while paused — only the embedding work stops.
    /// </summary>
    public sealed record GlobalState
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>
        /// When true, every running MCP server stops doing reindex work on its
        /// next poll. Search continues to work from the on-disk index. Default
        /// is false (indexing active).
        /// </summary>
        public bool IndexingPaused { get; init; }

        /// <summary>
        /// The folder that holds machine-wide akin state for the current user
        /// (<c>~/.akin</c> on Linux/macOS). Resolved at runtime rather than
        /// hardcoded so it adapts to the host and so tests can pass a temp
        /// folder instead.
        /// </summary>
        public static string DefaultStateFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".akin");

        /// <summary>
        /// Loads the current global state from <c>state.json</c> in the given
        /// folder. Returns defaults (indexing active) when the file is absent
        /// or unreadable.
        /// </summary>
        public static async Task<GlobalState> LoadAsync(string stateFolder, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateFolder);

            string path = Path.Combine(stateFolder, "state.json");

            if (!File.Exists(path))
                return new GlobalState();

            try
            {
                string json = await File.ReadAllTextAsync(path, cancellationToken);
                return JsonSerializer.Deserialize<GlobalState>(json, JsonOptions) ?? new GlobalState();
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                Console.Error.WriteLine($"[akin] failed to load global state, using defaults: {ex.Message}");
                return new GlobalState();
            }
        }

        /// <summary>
        /// Persists this state to <c>state.json</c> in the given folder.
        /// </summary>
        public async Task SaveAsync(string stateFolder, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateFolder);

            Directory.CreateDirectory(stateFolder);
            string path = Path.Combine(stateFolder, "state.json");
            string tempPath = path + ".tmp";

            // Write to a sibling temp file then rename over the target so a
            // concurrent reader (e.g. a watcher refresh) never observes a
            // half-written file. File.Move within the same folder is atomic.
            string json = JsonSerializer.Serialize(this, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, path, overwrite: true);
        }
    }
}
