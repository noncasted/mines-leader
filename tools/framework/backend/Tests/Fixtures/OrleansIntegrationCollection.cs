using Cluster.Discovery;
using Cluster.State;
using Common;
using Common.Extensions;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Infrastructure.State;
using Internal;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Orleans.TestingHost;
using Xunit;
using TaskScheduler = Infrastructure.Execution.TaskScheduler;

namespace Framework.Tests.Fixtures;

[CollectionDefinition(nameof(OrleansIntegrationCollection))]
public class OrleansIntegrationCollection : ICollectionFixture<OrleansTestClusterFixture>;
