using ClaimsModule.Application.Common.Interfaces;

namespace ClaimsModule.Infrastructure.Services;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
