using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

internal static class UnobservedTurnExtensions
{
    // The result of the call does not matter to the test, what it does to the grain does.
    public static void NoObserve(this Task task)
    {
        _ = task.ContinueWith(completed => _ = completed.Exception, TaskScheduler.Default);
    }
}
