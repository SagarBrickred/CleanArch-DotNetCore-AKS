namespace CleanArch.Domain.Common;

/// <summary>
/// Base entity providing identity and audit fields for every aggregate/entity in the system.
/// Soft-delete is enforced at this level so no entity can accidentally implement hard deletes.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    /// <summary>Soft-delete flag. Repositories must filter this via global query filters.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Optimistic concurrency token (maps to SQL Server rowversion).</summary>
    public byte[]? RowVersion { get; set; }

    private readonly List<BaseDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<BaseDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(BaseDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(BaseDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public abstract class BaseDomainEvent
{
    public DateTime OccurredOnUtc { get; protected set; } = DateTime.UtcNow;
}
