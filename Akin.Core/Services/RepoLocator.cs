namespace Akin.Core.Services
{
    /// <summary>
    /// Walks up from a starting path to find the enclosing git repository root
    /// (the directory containing a <c>.git</c> entry).
    /// </summary>
    public static class RepoLocator
    {
        public static string? FindRepoRoot(string startPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(startPath);

            DirectoryInfo? current = new DirectoryInfo(startPath);
            while (current != null)
            {
                string gitPath = Path.Combine(current.FullName, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                    return current.FullName;

                current = current.Parent;
            }

            return null;
        }
    }
}
