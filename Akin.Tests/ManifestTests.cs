using System.Text.Json;
using Akin.Core.Models;

namespace Akin.Tests
{
    public class ManifestTests
    {
        [Fact]
        public void Manifest_JsonRoundTrip_PreservesAllFields()
        {
            Manifest original = new Manifest
            {
                SchemaVersion = Manifest.CurrentSchemaVersion,
                EmbeddingModel = "nomic-embed-text-v1.5",
                EmbeddingDimension = 768,
                ChunkerFingerprint = "abc123",
                LastRebuiltUtc = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc),
            };

            string json = JsonSerializer.Serialize(original);
            Manifest? roundTripped = JsonSerializer.Deserialize<Manifest>(json);

            Assert.NotNull(roundTripped);
            Assert.Equal(original, roundTripped);
        }

        [Fact]
        public void CurrentSchemaVersion_IsPositive()
        {
            Assert.True(Manifest.CurrentSchemaVersion >= 1);
        }
    }
}
