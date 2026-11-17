using FluentAssertions;
using Infrastructure;
using NSubstitute;
using Orleans.Serialization.Invocation;
using Xunit;

namespace Framework.Tests.State;

public class TransactionResponseTests
{
    private readonly TransactionContext _context = new() { Id = Guid.NewGuid() };

    [Fact]
    public void Result_IsTheResultOfTheInnerResponse()
    {
        var response = TransactionResponse.Create(Response.FromResult(5), _context);

        response.Result.Should().Be(5);
        response.GetResult<int>().Should().Be(5);
        response.Result = 6;
        response.Result.Should().Be(6);
    }

    // The exception stays hidden from Orleans until the call filter has merged the context of the response.
    [Fact]
    public void Exception_HiddenUntilGetException()
    {
        var error = new InvalidOperationException("failed");
        var response = TransactionResponse.Create(Response.FromException(error), _context);

        response.Exception.Should().BeNull();
        response.GetException().Should().BeSameAs(error);

        var result = () => response.Result;
        result.Should().Throw<InvalidOperationException>().WithMessage("failed");
    }

    [Fact]
    public void Exception_SetGoesToTheInnerResponse()
    {
        var error = new InvalidOperationException("failed");
        var response = TransactionResponse.Create(Response.FromException(new InvalidOperationException("first")), _context);

        response.Exception = error;

        response.GetException().Should().BeSameAs(error);
    }

    [Fact]
    public void Dispose_DisposesTheInnerResponse()
    {
        var inner = Substitute.For<Response>();

        TransactionResponse.Create(inner, _context).Dispose();

        inner.Received(1).Dispose();
    }

    [Fact]
    public void TransactionResult_ToString_NamesTheOutcome()
    {
        new TransactionResult { IsSuccess = true }.ToString().Should().Be("TransactionResult(Success=True)");

        var error = new InvalidOperationException("failed");
        new TransactionResult { IsSuccess = false, Error = error }.ToString()
            .Should().Be($"TransactionResult(Success=False, Error={error})");
    }
}
