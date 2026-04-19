using Akin.Core.Models;

namespace Akin.Core.Interfaces
{
    /// <summary>
    /// Embeds a query and returns ranked file-level results.
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Runs a semantic search using the provided query text.
        /// </summary>
        /// <param name="query">The natural-language or keyword query.</param>
        /// <param name="options">Options controlling result shape.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ranked hits, best first.</returns>
        Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchOptions options, CancellationToken cancellationToken = default);
    }
}
