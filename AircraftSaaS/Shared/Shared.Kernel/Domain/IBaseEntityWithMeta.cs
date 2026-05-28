namespace Shared.Kernel.Domain;

public interface IBaseEntityWithMeta : IBaseEntityWithMeta<Guid>
{
}

public interface IBaseEntityWithMeta<TKey> : IBaseEntity<TKey>, IBaseEntityMeta 
    where TKey : IEquatable<TKey>
{
}
