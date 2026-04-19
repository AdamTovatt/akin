using System.Security.Cryptography;
using System.Text;
using Akin.Core.Interfaces;
using Akin.Core.Models;
using VectorSharp.Chunking;

namespace Akin.Core.Services
{
    /// <summary>
    /// Picks a <see cref="ChunkerConfig"/> for a file based on its extension or
    /// its filename for well-known extensionless files (Dockerfile, Makefile, etc.).
    /// Returns null when the file isn't in the allowlist so the caller can skip
    /// indexing it entirely rather than guessing at a chunking strategy and
    /// risking a binary-as-text disaster.
    /// </summary>
    public sealed class ChunkerSelector : IChunkerSelector
    {
        private const int DefaultMaxTokensPerChunk = 300;

        private readonly Dictionary<string, ChunkerConfig> _byExtension;
        private readonly HashSet<string> _knownTextExtensions;
        private readonly HashSet<string> _knownTextFilenames;
        private readonly HashSet<string> _filenameOnlyExtensions;
        private readonly ChunkerConfig _plainTextConfig;
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
            _plainTextConfig = Build("plaintext", BreakStrings.PlainText, StopSignals.PlainText);

            _byExtension = new Dictionary<string, ChunkerConfig>(StringComparer.OrdinalIgnoreCase)
            {
                [".md"] = markdown,
                [".markdown"] = markdown,
                [".mdx"] = markdown,
                [".cs"] = csharp,
                [".js"] = javascript,
                [".jsx"] = javascript,
                [".ts"] = javascript,
                [".tsx"] = javascript,
                [".mjs"] = javascript,
                [".cjs"] = javascript,
                [".html"] = html,
                [".htm"] = html,
                [".xhtml"] = html,
                [".css"] = css,
                [".scss"] = css,
                [".sass"] = css,
                [".less"] = css,
                [".py"] = python,
                [".pyi"] = python,
                [".pyw"] = python,
            };

            // A broader set of extensions we're willing to index as plain text.
            // Everything outside this set is skipped. We err on the side of missing
            // some esoteric-but-legitimate text files rather than inviting binary
            // content into the chunker.
            _knownTextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Plain text and docs
                ".txt", ".rst", ".adoc", ".asciidoc", ".text",
                // Shell and scripting
                ".sh", ".bash", ".zsh", ".fish", ".ps1", ".psm1", ".psd1", ".bat", ".cmd",
                // Other programming languages (chunked as plain text)
                ".fs", ".fsi", ".fsx", ".vb", ".go", ".rs", ".java", ".kt", ".kts", ".scala",
                ".groovy", ".swift", ".m", ".mm", ".c", ".cc", ".cpp", ".cxx",
                ".h", ".hh", ".hpp", ".hxx", ".php", ".phtml", ".pl", ".pm",
                ".sql", ".lua", ".r", ".dart", ".ex", ".exs", ".erl", ".hs", ".ml", ".mli",
                ".rb", ".erb", ".rake",
                // Structured data / config
                ".json", ".jsonc", ".json5",
                ".yaml", ".yml",
                ".toml",
                ".xml", ".xsd", ".xsl", ".xslt",
                ".ini", ".cfg", ".conf",
                ".env",
                ".properties",
                ".editorconfig",
                ".gitignore", ".gitattributes", ".gitmodules", ".gitconfig",
                ".dockerignore",
                ".prettierrc", ".eslintrc", ".babelrc",
                // Project / build
                ".csproj", ".vbproj", ".fsproj",
                ".sln", ".slnx",
                ".props", ".targets",
                ".nuspec",
                ".gradle", ".pom",
            };

            // Extensions we don't try to chunk (binary content, or text content
            // that's not useful to embed — path data, base64 blobs), but whose
            // filenames are worth indexing so queries like "app icon" or
            // "company logo" can surface them.
            _filenameOnlyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Raster images
                ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".heic", ".avif",
                ".ico", ".icns",
                // Vector / design
                ".svg", ".ai", ".eps", ".psd", ".sketch", ".fig", ".xd",
                // Fonts
                ".ttf", ".otf", ".woff", ".woff2", ".eot",
                // Documents
                ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx",
                // Audio / video
                ".mp3", ".wav", ".flac", ".ogg", ".m4a",
                ".mp4", ".mov", ".avi", ".webm", ".mkv",
                // Archives
                ".zip", ".tar", ".gz", ".tgz", ".bz2", ".7z", ".rar",
            };

            _knownTextFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Dockerfile", "Containerfile",
                "Makefile", "GNUmakefile",
                "Rakefile", "Gemfile", "Procfile", "Justfile", "Taskfile",
                "Jenkinsfile", "Vagrantfile", "Brewfile",
                "LICENSE", "LICENCE", "COPYING", "COPYRIGHT",
                "README", "CONTRIBUTING", "CONTRIBUTORS", "AUTHORS",
                "CHANGELOG", "CHANGES", "HISTORY",
                "NOTICE", "CODEOWNERS", "MAINTAINERS",
                "TODO", "INSTALL",
            };

            _fingerprint = ComputeFingerprint();
        }

        public ChunkerConfig? SelectFor(string relativePath)
        {
            ArgumentNullException.ThrowIfNull(relativePath);

            string extension = Path.GetExtension(relativePath);
            if (!string.IsNullOrEmpty(extension))
            {
                if (_byExtension.TryGetValue(extension, out ChunkerConfig? specific))
                    return specific;

                if (_knownTextExtensions.Contains(extension))
                    return _plainTextConfig;

                return null;
            }

            // No extension: check if the filename itself is a well-known text file.
            string filename = Path.GetFileName(relativePath);
            if (_knownTextFilenames.Contains(filename))
                return _plainTextConfig;

            return null;
        }

        public bool ShouldIndexByFilename(string relativePath)
        {
            ArgumentNullException.ThrowIfNull(relativePath);

            string extension = Path.GetExtension(relativePath);
            return !string.IsNullOrEmpty(extension) && _filenameOnlyExtensions.Contains(extension);
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
            builder.Append("v3|");
            builder.Append("max=").Append(DefaultMaxTokensPerChunk).Append('|');

            foreach (KeyValuePair<string, ChunkerConfig> pair in _byExtension.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                builder.Append("ext:").Append(pair.Key).Append('=').Append(pair.Value.FormatName).Append(';');
            }

            builder.Append("plain-ext:");
            foreach (string ext in _knownTextExtensions.OrderBy(e => e, StringComparer.Ordinal))
            {
                builder.Append(ext).Append(',');
            }

            builder.Append("plain-file:");
            foreach (string name in _knownTextFilenames.OrderBy(n => n, StringComparer.Ordinal))
            {
                builder.Append(name).Append(',');
            }

            builder.Append("filename-only-ext:");
            foreach (string ext in _filenameOnlyExtensions.OrderBy(e => e, StringComparer.Ordinal))
            {
                builder.Append(ext).Append(',');
            }

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
            return Convert.ToHexString(hash);
        }
    }
}
