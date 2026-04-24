using System.Text.Json;
using System.Text.Json.Serialization;

namespace Akin.Core.Models
{
    /// <summary>
    /// Custom converter that reads both the current <c>LastIndexUpdateUtc</c> property
    /// and the legacy <c>LastRebuiltUtc</c> name for backwards compatibility with
    /// existing on-disk manifests. Always writes the new name.
    /// </summary>
    internal sealed class ManifestJsonConverter : JsonConverter<Manifest>
    {
        public override Manifest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            int? schemaVersion = null;
            string? embeddingModel = null;
            int? embeddingDimension = null;
            string? chunkerFingerprint = null;
            DateTime? lastIndexUpdateUtc = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException();

                string propertyName = reader.GetString() ?? throw new JsonException("Null property name");
                reader.Read();

                switch (propertyName)
                {
                    case nameof(Manifest.SchemaVersion):
                        schemaVersion = reader.GetInt32();
                        break;
                    case nameof(Manifest.EmbeddingModel):
                        embeddingModel = reader.GetString();
                        break;
                    case nameof(Manifest.EmbeddingDimension):
                        embeddingDimension = reader.GetInt32();
                        break;
                    case nameof(Manifest.ChunkerFingerprint):
                        chunkerFingerprint = reader.GetString();
                        break;
                    case nameof(Manifest.LastIndexUpdateUtc) or "LastRebuiltUtc":
                        lastIndexUpdateUtc = reader.GetDateTime();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new Manifest
            {
                SchemaVersion = schemaVersion ?? throw new JsonException("Missing SchemaVersion"),
                EmbeddingModel = embeddingModel ?? throw new JsonException("Missing EmbeddingModel"),
                EmbeddingDimension = embeddingDimension ?? throw new JsonException("Missing EmbeddingDimension"),
                ChunkerFingerprint = chunkerFingerprint ?? throw new JsonException("Missing ChunkerFingerprint"),
                LastIndexUpdateUtc = lastIndexUpdateUtc ?? throw new JsonException("Missing LastIndexUpdateUtc"),
            };
        }

        public override void Write(Utf8JsonWriter writer, Manifest value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber(nameof(Manifest.SchemaVersion), value.SchemaVersion);
            writer.WriteString(nameof(Manifest.EmbeddingModel), value.EmbeddingModel);
            writer.WriteNumber(nameof(Manifest.EmbeddingDimension), value.EmbeddingDimension);
            writer.WriteString(nameof(Manifest.ChunkerFingerprint), value.ChunkerFingerprint);
            writer.WriteString(nameof(Manifest.LastIndexUpdateUtc), value.LastIndexUpdateUtc);
            writer.WriteEndObject();
        }
    }
}
