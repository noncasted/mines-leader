using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.ClientTelemetry;
using Infrastructure;
using Infrastructure.State;
using Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

[CollectionDefinition(nameof(ClientTelemetryCollection))]
public class ClientTelemetryCollection : ICollectionFixture<ClientTelemetryClusterFixture>;
