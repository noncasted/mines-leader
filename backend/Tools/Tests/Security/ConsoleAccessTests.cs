using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Orchestration;
using Xunit;

namespace Tests.Security;

/// <summary>
/// F1.4: an empty or development console token turns authentication off only in Development.
/// </summary>
public class ConsoleAccessTests
{
    [Theory]
    [InlineData("Production", "")]
    [InlineData("Production", ConsoleAccess.DevelopmentToken)]
    [InlineData("Staging", "")]
    [InlineData("Staging", ConsoleAccess.DevelopmentToken)]
    public void OutsideDevelopment_EmptyOrDevelopmentToken_FailsStartup(string environment, string token)
    {
        var read = () => Read(environment, token);

        read.Should().Throw<InvalidOperationException>().WithMessage($"*{ConsoleAccess.TokenKey}*{environment}*");
    }

    [Fact]
    public void OutsideDevelopment_NoTokenAtAll_FailsStartup()
    {
        var read = () => ConsoleAccess.Read(new ConfigurationBuilder().Build(), Environment("Production"));

        read.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(ConsoleAccess.DevelopmentToken)]
    public void Development_EmptyOrDevelopmentToken_DisablesTheCheck(string token)
    {
        var access = Read("Development", token);

        access.IsEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void RealToken_EnablesTheCheck(string environment)
    {
        var access = Read(environment, "secret-token");

        access.IsEnabled.Should().BeTrue();
        access.IsValid("secret-token").Should().BeTrue();
        access.IsValid("secret-tokem").Should().BeFalse();
        access.IsValid("secret").Should().BeFalse();
        access.IsValid("").Should().BeFalse();
        access.IsValid(null).Should().BeFalse();
    }

    [Fact]
    public void Disabled_IsValid_Throws()
    {
        var access = Read("Development", ConsoleAccess.DevelopmentToken);

        var check = () => access.IsValid("anything");

        check.Should().Throw<InvalidOperationException>();
    }

    internal static ConsoleAccess Read(string environment, string token)
    {
        var configuration = new ConfigurationBuilder()
                            .AddInMemoryCollection([new KeyValuePair<string, string?>(ConsoleAccess.TokenKey, token)])
                            .Build();

        return ConsoleAccess.Read(configuration, Environment(environment));
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }
}
