namespace Shared.Kernel.Domain;

public interface IBaseEntity : IBaseEntity<Guid>
{
}

public interface IBaseEntity<TKey>
    where TKey : IEquatable<TKey>
{
    public TKey Id { get; set; }
}
