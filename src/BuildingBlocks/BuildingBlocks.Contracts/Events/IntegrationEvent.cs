namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Base record implementation providing default immutability, equality,
/// and telemetry metadata for integration events.
/// </summary>
public abstract record IntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    string CorrelationId,
    string? CausationId = null) : IIntegrationEvent
{
    protected IntegrationEvent() 
        : this(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid().ToString(), null)
    {
    }

    protected IntegrationEvent(string correlationId, string? causationId = null)
        : this(Guid.NewGuid(), DateTime.UtcNow, correlationId, causationId)
    {
    }
}
