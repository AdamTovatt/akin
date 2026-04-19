using System.Text;
using VectorSharp.Embedding;

namespace Akin.Tests.Fakes
{
    /// <summary>
    /// Deterministic, dependency-free embedding provider for tests. Maps each input
    /// string to a fixed-dimension float vector using a simple hash of character codes,
    /// then L2-normalizes the result so cosine similarity is well-behaved.
    /// </summary>
    internal sealed class FakeEmbeddingProvider : IEmbeddingProvider
    {
        public int Dimension { get; }

        public FakeEmbeddingProvider(int dimension = 16)
        {
            Dimension = dimension;
        }

        public Task<float[]> EmbedAsync(string text, EmbeddingPurpose purpose = EmbeddingPurpose.Document, CancellationToken cancellationToken = default)
        {
            float[] vector = new float[Dimension];
            ReadOnlySpan<char> span = text.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                int slot = span[i] % Dimension;
                vector[slot] += 1.0f;
            }

            // Include purpose so query and document vectors differ slightly, matching
            // the real provider's behavior where purpose prefixes the input.
            int purposeSlot = (int)purpose % Dimension;
            vector[purposeSlot] += 0.1f;

            // L2 normalize so cosine similarity stays in [-1, 1].
            float magnitude = 0;
            for (int i = 0; i < Dimension; i++)
                magnitude += vector[i] * vector[i];
            magnitude = MathF.Sqrt(magnitude);
            if (magnitude > 0)
            {
                for (int i = 0; i < Dimension; i++)
                    vector[i] /= magnitude;
            }

            return Task.FromResult(vector);
        }

        public void Dispose()
        {
        }
    }
}
