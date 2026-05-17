using Base.Contracts.Domain;

namespace Base.Domain;

public abstract class BaseEntity : BaseEntity<Guid>, IBaseEntity
{
    public BaseEntity()
    {
        Id = Guid.NewGuid();
    }
}

public abstract class BaseEntity<TKey> : IBaseEntity<TKey> 
    where TKey : IEquatable<TKey>
{
    public virtual TKey Id { get; set; } = default!;
}
