namespace Shared.Kernel.Domain;

public interface IBaseEntityAppUser : IBaseEntityAppUser<Guid>
{
}

public interface IBaseEntityAppUser<TKey>
    where TKey : IEquatable<TKey>
{
    public TKey AppUserId { get; set; }
}
