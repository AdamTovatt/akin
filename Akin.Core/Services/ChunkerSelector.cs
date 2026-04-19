using System.Security.Cryptography;
using System.Text;
using Akin.Core.Interfaces;
using Akin.Core.Models;
using VectorSharp.Chunking;

namespace Akin.Core.Services
{
    /// <summary>
    /// Picks a <see cref="ChunkerConfig"/> for a file based on its extension,
    /// falling back to plain-text for unknown or extensionless files.
    /// </summary>
    public sealed class ChunkerSelector : IChunkerSelector
    {
        private const int DefaultMaxTokensPerChunk = 300;

        private readonly Dictionary<string, ChunkerConfig> _byExtension;
        private readonly ChunkerConfig _fallback;
        private readonly string _fingerprint;

        public string Fingerprint => _fingerprint;

        public ChunkerSelector()
        {
            ChunkerConfig markdown = Build("markdown", BreakStrings.Markdown, StopSignals.Markdown);
            ChunkerConfig csharp = Build("csharp", BreakStrings.CSharp, StopSignals.CSharp);
            ChunkerConfig javascript = Build("javascript", BreakStrings.JavaScript, StopSignals.JavaScript);
            ChunkerConfig html = Build("html", BreakStrings.Html, StopSignals.Html);
            ChunkerConfig css = Build("css", BreakStrings.Css, StopSignals.Css);
            ChunkerConfig python = Build("python", BreakStrings.Python, StopSignals.Python);
            _fallback = Build("plaintext", BreakStrings.PlainText, StopSignals.PlainText);

            _byExtension = new Dictionary<string, ChunkerConfig>(StringComparer.OrdinalIgnoreCase)
            {
                [".md"] = markdown,
                [".markdown"] = markdown,
                [".cs"] = csharp,
                [".js"] = javascript,
                [".jsx"] = javascript,
                [".ts"] = javascript,
                [".tsx"] = javascript,
                [".mjs"] = javascript,
                [".cjs"] = javascript,
                [".html"] = html,
                [".htm"] = html,
                [".css"] = css,
                [".scss"] = css,
                [".py"] = python,
                [".pyi"] = python,
            };

            _fingerprint = ComputeFingerprint();
        }

        public ChunkerConfig SelectFor(string relativePath)
        {
            ArgumentNullException.ThrowIfNull(relativePath);

            string extension = Path.GetExtension(relativePath);
            if (!string.IsNullOrEmpty(extension) && _byExtension.TryGetValue(extension, out ChunkerConfig? specific))
            {
                return specific;
            }

            return _fallback;
        }

        private static ChunkerConfig Build(string name, IReadOnlyList<string> breakStrings, IReadOnlyList<string> stopSignals)
        {
            return new ChunkerConfig
            {
                FormatName = name,
                BreakStrings = breakStrings,
                StopSignals = stopSignals,
                MaxTokensPerChunk = DefaultMaxTokensPerChunk,
            };
        }

        private string ComputeFingerprint()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("v1|");
            builder.Append("max=").Append(DefaultMaxTokensPerChunk).Append('|');

            foreach (KeyValuePair<string, ChunkerConfig> pair in _byExtension.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                builder.Append(pair.Key).Append('=').Append(pair.Value.FormatName).Append(';');
            }
            builder.Append("fallback=").Append(_fallback.FormatName);

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
            return Convert.ToHexString(hash);
        }
    }
}
