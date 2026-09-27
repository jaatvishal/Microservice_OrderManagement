using BuildingBlocks.Application.Common;

namespace BuildingBlocks.Infrastructure.Common;

public sealed class CorrelationContextAccessor : ICorrelationContextAccessor
{
    private static readonly AsyncLocal<string?> _currentCorrelationId = new();

    public string CorrelationId
    {
        get => _currentCorrelationId.Value ?? Guid.NewGuid().ToString();
        private set => _currentCorrelationId.Value = value;
    }

    public void SetCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId cannot be null or empty.", nameof(correlationId));

        _currentCorrelationId.Value = correlationId;
    }
}
