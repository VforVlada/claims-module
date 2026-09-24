using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

public sealed class CauseOfLossCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PerilCategory { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
