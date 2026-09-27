namespace BuildingBlocks.Contracts.Events;

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity);

public record OrderCreatedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items,
    string CorrelationId,
    string? CausationId = null) 
    : IntegrationEvent(CorrelationId, CausationId);

public record InventoryReservedIntegrationEvent(
    Guid OrderId,
    string CorrelationId,
    string? CausationId = null) 
    : IntegrationEvent(CorrelationId, CausationId);

public record InventoryReservationFailedIntegrationEvent(
    Guid OrderId,
    string Reason,
    string CorrelationId,
    string? CausationId = null) 
    : IntegrationEvent(CorrelationId, CausationId);

public record PaymentCompletedIntegrationEvent(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string CorrelationId,
    string? CausationId = null) 
    : IntegrationEvent(CorrelationId, CausationId);

public record PaymentFailedIntegrationEvent(
    Guid OrderId,
    string Reason,
    string CorrelationId,
    string? CausationId = null) 
    : IntegrationEvent(CorrelationId, CausationId);

public record OrderConfirmedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    string CorrelationId,
    string? CausationId = null) 
    : IntegrationEvent(CorrelationId, CausationId);

public record OrderCancelledIntegrationEvent(
    Guid OrderId,
    string Reason,
    string CorrelationId,
    string? CausationId = null) 
    : IntegrationEvent(CorrelationId, CausationId);
