using Xunit;

namespace HybridDb.Tests
{
    [CollectionDefinition(nameof(DisableParallelizationCollection), DisableParallelization = true)]
    public class DisableParallelizationCollection { }
}