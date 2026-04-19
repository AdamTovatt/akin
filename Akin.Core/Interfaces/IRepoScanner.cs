namespace Akin.Core.Interfaces
{
    /// <summary>
    /// Enumerates the files in a repository that should be considered for indexing.
    /// The canonical implementation runs <c>git ls-files</c> so we get the same set
    /// of files that git tracks, automatically respecting <c>.gitignore</c>.
    /// </summary>
    public interface IRepoScanner
    {
        /// <summary>
        /// Returns repo-relative paths for all files that should be considered for
        /// indexing. Paths use forward slashes and are sorted for stable ordering.
        /// </summary>
        Task<IReadOnlyList<string>> ScanAsync(CancellationToken cancellationToken = default);
    }
}
