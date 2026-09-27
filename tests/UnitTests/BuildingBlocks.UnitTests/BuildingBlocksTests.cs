using BuildingBlocks.Application.Models;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Infrastructure.Common;

namespace BuildingBlocks.UnitTests;

public class BuildingBlocksTests
{
    [Fact]
    public void Result_Success_ShouldHaveSuccessState()
    {
        var result = Result.Success("TestData");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("TestData", result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Result_Failure_ShouldHaveErrorState()
    {
        var error = Error.NotFound("Resource.NotFound", "The requested resource was not found.");
        var result = Result.Failure<string>(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void IntegrationEvent_ShouldInitializeWithUniqueIdsAndCorrelation()
    {
        var correlationId = Guid.NewGuid().ToString();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var @event = new OrderConfirmedIntegrationEvent(orderId, customerId, correlationId);

        Assert.NotEqual(Guid.Empty, @event.EventId);
        Assert.Equal(correlationId, @event.CorrelationId);
        Assert.True(@event.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void CorrelationContextAccessor_ShouldSetAndRetrieveCorrelationId()
    {
        var accessor = new CorrelationContextAccessor();
        var expectedId = "trace-" + Guid.NewGuid().ToString();

        accessor.SetCorrelationId(expectedId);

        Assert.Equal(expectedId, accessor.CorrelationId);
    }
}
