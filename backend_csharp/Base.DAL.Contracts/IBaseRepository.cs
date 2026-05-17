using Base.Contracts.Domain;
using Base.Domain;

namespace Base.DAL.Contracts;

public interface IBaseRepository<TEntity> : IBaseRepository<Guid, TEntity>
    where TEntity : IBaseEntity<Guid>
{
    
}

public interface IBaseRepository<in TKey, TEntity>
    where TKey : IEquatable<TKey>
    where TEntity : IBaseEntity<TKey>
{
    Task<IEnumerable<TEntity>> AllAsync(TKey appUserId = default!, Guid? companyId = null);
    
    Task<TEntity?> FindAsync(TKey id, TKey appUserId = default!, Guid? companyId = null);

    void Add(TEntity entity);

    Task<TEntity?> UpdateAsync(TEntity entity, TKey appUserId = default!, Guid? companyId = null);
    
    void Remove(TEntity entity);
    Task RemoveAsync(TKey id, TKey appUserId = default!, Guid? companyId = null);
}