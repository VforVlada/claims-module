using ClaimsModule.Application.Common.Interfaces;

namespace ClaimsModule.Infrastructure.Tests.TestHelpers;

public sealed class FakeDateTimeProvider(DateTimeOffset utcNow) : IDateTimeProvider
{
    public FakeDateTimeProvider() : this(DateTimeOffset.UtcNow)
    {
    }

    public DateTimeOffset UtcNow { get; set; } = utcNow;
}
