using BuildingBlocks.Application.Common;

namespace BuildingBlocks.Infrastructure.Common;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
