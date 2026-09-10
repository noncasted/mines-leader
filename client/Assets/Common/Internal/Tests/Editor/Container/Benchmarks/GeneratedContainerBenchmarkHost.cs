using System;

namespace Internal.Tests
{
    internal static class GeneratedContainerBenchmarkHost
    {
        public const string ContainerName = "Generated";

        public static BenchmarkReport Run()
        {
            return ContainerBenchmarkRunner.Run(ContainerName, Open);
        }

        public static IBenchmarkSession Open()
        {
            throw new InvalidOperationException(
                "Generated class for the synthetic graph is not available until steps 1, 1c and 2. " +
                "Do not invent numbers.");
        }
    }
}
