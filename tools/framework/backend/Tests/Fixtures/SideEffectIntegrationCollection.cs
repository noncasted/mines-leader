using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Framework.Tests.Fixtures;

[CollectionDefinition(nameof(SideEffectIntegrationCollection))]
public class SideEffectIntegrationCollection : ICollectionFixture<SideEffectTestFixture>;
