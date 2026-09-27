namespace BuildingBlocks.Application.Common;

/// <summary>
/// Abstraction to access ambient correlation identifiers propagated via HTTP headers or message metadata.
/// </summary>
public interface ICorrelationContextAccessor
{
    string CorrelationId { get; }
    void SetCorrelationId(string correlationId);
}
