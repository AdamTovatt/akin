namespace Akin.Core.Models
{
    /// <summary>
    /// Options controlling how a search is executed and how its results are shaped.
    /// </summary>
    public sealed record SearchOptions
    {
        /// <summary>
        /// The maximum number of files to return. Chunks are grouped by file after
        /// retrieval, so the underlying vector search pulls a larger candidate pool.
        /// </summary>
        public int MaxResults { get; init; } = 10;

        /// <summary>
        /// When true, each matching region carries the full chunk text. When false,
        /// regions only include path, line range, and score. Defaults to true.
        /// </summary>
        public bool IncludeSnippets { get; init; } = true;

        /// <summary>
        /// Glob patterns (relative to the repository root) that a file's path must
        /// match for its chunks to be considered. When empty, all files are eligible
        /// (subject to <see cref="ExcludePaths"/>). Supports <c>*</c>, <c>?</c>, and
        /// <c>**</c> using the semantics of
        /// <c>Microsoft.Extensions.FileSystemGlobbing</c>.
        /// </summary>
        public IReadOnlyList<string> IncludePaths { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Glob patterns (relative to the repository root) that exclude matching files
        /// from the search even when they also match an include pattern.
        /// </summary>
        public IReadOnlyList<string> ExcludePaths { get; init; } = Array.Empty<string>();

        /// <summary>
        /// File kinds to include in the search. When empty, all kinds are eligible.
        /// Combines with path filters: a chunk must satisfy both the path globs and
        /// (if set) match one of these kinds.
        /// </summary>
        public IReadOnlyList<FileKind> IncludeKinds { get; init; } = Array.Empty<FileKind>();
    }
}
