namespace Base.Contracts.Domain;

// declares that entity belongs to some app user (i.e. there is a relationship defined via this fk value)
// this allows automatic universal filtering in dbcontext (IDOR)
public interface IAppUserId : IAppUserId<Guid>
{
    
}
public interface IAppUserId<TKey>
    where TKey : IEquatable<TKey>
{
    public TKey AppUserId { get; set; }
}
