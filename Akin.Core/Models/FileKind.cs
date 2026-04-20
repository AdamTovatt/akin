namespace Akin.Core.Models
{
    /// <summary>
    /// A coarse categorisation of a file, derived from its extension or well-known
    /// filename, used to let callers scope a search to particular kinds of files
    /// (e.g. code only, or code + config but not docs).
    /// </summary>
    public enum FileKind
    {
        /// <summary>
        /// Source code: <c>.cs</c>, <c>.ts</c>, <c>.py</c>, <c>.go</c>, shell scripts,
        /// HTML/CSS, and similar programming-language files.
        /// </summary>
        Code,

        /// <summary>
        /// Human-readable prose: <c>.md</c>, <c>.txt</c>, <c>.rst</c>, and well-known
        /// text filenames like <c>README</c>, <c>LICENSE</c>, <c>CHANGELOG</c>.
        /// </summary>
        Docs,

        /// <summary>
        /// Machine-readable configuration, project/build files, and tracked binary
        /// assets: <c>.json</c>, <c>.yaml</c>, <c>.toml</c>, <c>.csproj</c>,
        /// <c>Dockerfile</c>, <c>Makefile</c>, images, fonts, and similar.
        /// </summary>
        Config,
    }

    /// <summary>
    /// Parsing helpers for the user-facing <see cref="FileKind"/> tokens accepted
    /// by the CLI <c>--type</c> flag and the MCP <c>includeTypes</c> parameter.
    /// Single source of truth so both surfaces accept the same aliases.
    /// </summary>
    public static class FileKinds
    {
        /// <summary>
        /// Accepted spellings (case-insensitive): <c>code</c>, <c>docs</c>/<c>doc</c>,
        /// <c>config</c>/<c>cfg</c>. Returns <c>false</c> for any other input.
        /// </summary>
        public static bool TryParse(string value, out FileKind kind)
        {
            if (value == null)
            {
                kind = default;
                return false;
            }
            switch (value.ToLowerInvariant())
            {
                case "code":
                    kind = FileKind.Code;
                    return true;
                case "docs":
                case "doc":
                    kind = FileKind.Docs;
                    return true;
                case "config":
                case "cfg":
                    kind = FileKind.Config;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        /// <summary>
        /// The user-visible list of valid <c>--type</c>/<c>includeTypes</c> values,
        /// for use in error messages.
        /// </summary>
        public const string AcceptedValuesMessage = "code, docs, config";
    }
}
