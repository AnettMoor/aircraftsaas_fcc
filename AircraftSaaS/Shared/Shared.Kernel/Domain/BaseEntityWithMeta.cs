namespace Shared.Kernel.Domain;

public abstract class BaseEntityWithMeta : BaseEntityWithMeta<Guid>, IBaseEntityWithMeta
{
}

public abstract class BaseEntityWithMeta<TKey> : BaseEntity<TKey>, IBaseEntityWithMeta<TKey>
    where TKey : IEquatable<TKey>
{
    public virtual string CreatedBy { get; set; } = default!;
    public virtual DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual string? UpdatedBy { get; set; }
    public virtual DateTime? UpdatedAt { get; set; }
}
