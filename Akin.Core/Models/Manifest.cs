using System.Text.Json.Serialization;

namespace Akin.Core.Models
{
    /// <summary>
    /// Metadata describing the configuration used to build the current index.
    /// Persisted as <c>manifest.json</c> in the <c>.akin</c> folder. On startup the
    /// indexer compares the stored manifest to its current configuration and triggers
    /// a full rebuild if anything has changed.
    /// </summary>
    [JsonConverter(typeof(ManifestJsonConverter))]
    public sealed record Manifest
    {
        /// <summary>
        /// The current schema version of <c>.akin</c> layout. Incremented when the
        /// on-disk layout changes in an incompatible way.
        /// </summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>
        /// The schema version of the stored index.
        /// </summary>
        public required int SchemaVersion { get; init; }

        /// <summary>
        /// The embedding model identifier (e.g. "nomic-embed-text-v1.5").
        /// </summary>
        public required string EmbeddingModel { get; init; }

        /// <summary>
        /// The vector dimension produced by the embedding model.
        /// </summary>
        public required int EmbeddingDimension { get; init; }

        /// <summary>
        /// A stable hash over all chunker configurations used by the indexer. A mismatch
        /// indicates the chunking strategy has changed and the index should be rebuilt.
        /// </summary>
        public required string ChunkerFingerprint { get; init; }

        /// <summary>
        /// UTC timestamp of the last time the index was persisted to disk,
        /// whether from a full rebuild or an incremental update.
        /// </summary>
        public required DateTime LastIndexUpdateUtc { get; init; }
    }
}
