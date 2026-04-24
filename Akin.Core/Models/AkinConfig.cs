using System.Text.Json;
using System.Text.Json.Serialization;

namespace Akin.Core.Models
{
    /// <summary>
    /// User-facing configuration loaded from <c>.akin/config.json</c>. Controls
    /// resource usage during indexing. When the file does not exist or a property
    /// is omitted, sensible low-CPU defaults are used.
    /// </summary>
    public sealed record AkinConfig
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>
        /// Target maximum CPU usage percentage for background indexing (1–100).
        /// Controls both the number of ONNX inference threads and dynamic
        /// throttling between embedding calls. Default is 10 (nearly invisible).
        /// </summary>
        public int MaxCpuPercent { get; init; } = 10;

        /// <summary>
        /// Returns the number of ONNX intra-op threads derived from
        /// <see cref="MaxCpuPercent"/> and the available processor count.
        /// </summary>
        [JsonIgnore]
        public int DerivedIntraOpNumThreads
        {
            get
            {
                int clamped = Math.Clamp(MaxCpuPercent, 1, 100);
                int threads = (int)Math.Round(Environment.ProcessorCount * clamped / 100.0);
                return Math.Max(1, threads);
            }
        }

        /// <summary>
        /// Loads configuration from <c>config.json</c> in the given index folder.
        /// Returns defaults if the file does not exist or cannot be parsed.
        /// </summary>
        public static async Task<AkinConfig> LoadAsync(string indexFolder, CancellationToken cancellationToken = default)
        {
            string path = Path.Combine(indexFolder, "config.json");

            if (!File.Exists(path))
                return new AkinConfig();

            try
            {
                string json = await File.ReadAllTextAsync(path, cancellationToken);
                return JsonSerializer.Deserialize<AkinConfig>(json, JsonOptions) ?? new AkinConfig();
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                Console.Error.WriteLine($"[akin] failed to load config.json, using defaults: {ex.Message}");
                return new AkinConfig();
            }
        }

        /// <summary>
        /// Persists this configuration to <c>config.json</c> in the given index folder.
        /// </summary>
        public async Task SaveAsync(string indexFolder, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(indexFolder);
            string path = Path.Combine(indexFolder, "config.json");
            string json = JsonSerializer.Serialize(this, JsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }
    }
}
