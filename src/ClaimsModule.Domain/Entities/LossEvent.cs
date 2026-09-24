using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

public sealed class LossEvent : BaseEntity
{
    public Guid ClaimId { get; set; }

    public DateTimeOffset LossDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public Guid CauseOfLossCodeId { get; set; }

    public CauseOfLossCode? CauseOfLossCode { get; set; }
}
