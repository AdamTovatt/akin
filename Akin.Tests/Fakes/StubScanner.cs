using Akin.Core.Interfaces;

namespace Akin.Tests.Fakes
{
    internal sealed class StubScanner : IRepoScanner
    {
        public List<string> Files { get; set; } = new List<string>();

        public Task<IReadOnlyList<string>> ScanAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(Files.ToList());
        }
    }
}
