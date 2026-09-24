namespace ClaimsModule.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationEntityId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string UserCreated { get; set; } = string.Empty;

    public string? UserModified { get; set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public void MarkDeleted(DateTimeOffset when)
    {
        IsDeleted = true;
        DeletedAt = when;
    }
}
