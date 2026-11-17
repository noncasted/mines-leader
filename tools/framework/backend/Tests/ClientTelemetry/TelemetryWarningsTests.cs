using FluentAssertions;
using Game.ClientTelemetry;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// Warnings of the log of Unity travel with the errors but are counted apart: "errors 0" stays a statement about the errors.
public class TelemetryWarningsTests
{
    [Fact]
    public void AWarning_IsParsed_AsItsOwnLevel()
    {
        var batch = TelemetryBatchParser.Parse(TelemetryWire.Batch(Guid.NewGuid(), errors: [TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Warning, "Texture is too large")]));

        batch.Errors.Single().Level.Should().Be(TelemetryErrorLevel.Warning);
    }

    [Fact]
    public void TheDigest_CountsWarningsApart()
    {
        var buffer = TelemetrySessionBuffer.Empty(new ClientTelemetryStorageOptions());

        buffer.Apply(new TelemetryBatch
        {
            Names = [],
            Samples = [],
            Marks = [],
            Errors =
            [
                new TelemetryError(1, TelemetryErrorLevel.Error, "a", "", 2),
                new TelemetryError(2, TelemetryErrorLevel.Warning, "w", "", 5),
                new TelemetryError(3, TelemetryErrorLevel.Warning, "v", "", 1)
            ]
        }, DateTime.UtcNow);

        var digest = TelemetrySummaryCalculator.Digest(buffer);

        digest.ErrorCount.Should().Be(2);
        digest.ErrorRows.Should().Be(1);
        digest.WarningCount.Should().Be(6);
        digest.ErrorGroups.Groups.Should().HaveCount(3);
    }
}
