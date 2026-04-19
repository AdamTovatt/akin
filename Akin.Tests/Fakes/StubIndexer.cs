using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Tests.Fakes
{
    /// <summary>
    /// Captures calls to <see cref="ReindexFileAsync"/> so tests can verify exactly
    /// which files the reconciler decided to update.
    /// </summary>
    internal sealed class StubIndexer : IIndexer
    {
        public List<string> ReindexedFiles { get; } = new List<string>();
        public int FullReindexes { get; private set; }

        public Task ReindexAllAsync(IProgress<IndexProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            FullReindexes++;
            return Task.CompletedTask;
        }

        public Task ReindexFileAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            ReindexedFiles.Add(relativePath);
            return Task.CompletedTask;
        }
    }
}
