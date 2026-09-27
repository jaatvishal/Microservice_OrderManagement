namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Root contract for all integration events crossing microservice boundaries.
/// Contains distributed tracing and telemetry envelope metadata.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Unique identifier for this discrete event message.
    /// Used by idempotent consumers to detect and drop duplicate deliveries.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// UTC timestamp when the event was generated at the source.
    /// </summary>
    DateTime OccurredOnUtc { get; }

    /// <summary>
    /// Trace identifier that flows across synchronous and asynchronous hops
    /// to correlate all actions belonging to a single client transaction.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Identifier of the specific command or parent event that caused this event.
    /// Enables reconstructing causation trees during distributed incident debugging.
    /// </summary>
    string? CausationId { get; }
}
