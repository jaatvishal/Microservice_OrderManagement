using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Application.EventBus;

/// <summary>
/// Abstraction for publishing integration events to an asynchronous message broker
/// (e.g., Azure Service Bus in production, RabbitMQ or In-Memory in dev/tests).
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
