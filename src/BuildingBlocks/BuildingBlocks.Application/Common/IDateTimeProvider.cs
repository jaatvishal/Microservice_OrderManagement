namespace BuildingBlocks.Application.Common;

/// <summary>
/// Abstraction over system clock to ensure deterministic testability
/// and enforce UTC standardization across distributed microservices.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
