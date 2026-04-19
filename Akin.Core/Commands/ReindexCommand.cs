using System.Diagnostics;
using Akin.Core.Interfaces;
using Akin.Core.Models;

namespace Akin.Core.Commands
{
    /// <summary>
    /// Forces a full rebuild of the index regardless of whether the current one
    /// is compatible. Useful when the user suspects drift or wants to reset.
    /// </summary>
    public sealed class ReindexCommand : ICommand
    {
        private readonly IIndexer _indexer;
        private readonly IIndexStore _store;

        public ReindexCommand(IIndexer indexer, IIndexStore store)
        {
            ArgumentNullException.ThrowIfNull(indexer);
            ArgumentNullException.ThrowIfNull(store);

            _indexer = indexer;
            _store = store;
        }

        public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            Stopwatch sw = Stopwatch.StartNew();
            await _indexer.ReindexAllAsync(cancellationToken);
            sw.Stop();

            IndexStatus status = _store.GetStatus();
            string message = $"Reindex complete in {sw.Elapsed.TotalSeconds:0.0}s. " +
                             $"{status.FileCount} files, {status.ChunkCount} chunks.";
            return new CommandResult(true, message);
        }
    }
}
