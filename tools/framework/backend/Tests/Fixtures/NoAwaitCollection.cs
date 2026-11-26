using Xunit;

namespace Framework.Tests.Fixtures;

// The handler of NoAwaitExceptions is process-wide: the tests that replace it run alone, not in parallel with any other.
[CollectionDefinition(nameof(NoAwaitCollection), DisableParallelization = true)]
public class NoAwaitCollection;
