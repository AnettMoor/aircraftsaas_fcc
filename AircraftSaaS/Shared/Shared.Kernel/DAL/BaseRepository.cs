using Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Shared.Kernel.DAL;

public class BaseRepository<TDALEntity, TDomainEntity, TDbContext> : BaseRepository<Guid, TDALEntity, TDomainEntity, TDbContext>
    where TDALEntity : class, IBaseEntity<Guid>
    where TDomainEntity : class, IBaseEntity<Guid>
    where TDbContext : DbContext
{
    public BaseRepository(TDbContext repositoryDbContext, IBaseMapper<TDALEntity, TDomainEntity> mapper) :
        base(repositoryDbContext, mapper)
    {
        
    }
}


public class BaseRepository<TKey, TDALEntity, TDomainEntity, TDbContext> : IBaseRepository<TKey, TDALEntity>
    where TKey : IEquatable<TKey>
    where TDALEntity : class, IBaseEntity<TKey>
    where TDomainEntity : class, IBaseEntity<TKey>
    where TDbContext : DbContext
{
    protected readonly TDbContext RepositoryDbContext;
    protected readonly DbSet<TDomainEntity> RepositoryDbSet;
    protected readonly IBaseMapper<TDALEntity, TDomainEntity> Mapper;
    
    public BaseRepository(TDbContext repositoryDbContext, IBaseMapper<TDALEntity, TDomainEntity> mapper)
    {
        RepositoryDbContext = repositoryDbContext;
        Mapper = mapper;
        RepositoryDbSet = repositoryDbContext.Set<TDomainEntity>();
    }
    
    /// <summary>
    /// Returns a filtered IQueryable that applies IDOR filters based on the domain entity's
    /// interface implementations (IAppUserId, ICompanyEntity).
    /// Concrete repositories should call this instead of raw RepositoryDbSet to get automatic IDOR filtering.
    /// </summary>
    protected IQueryable<TDomainEntity> GetFilteredQuery(TKey appUserId = default!, Guid? companyId = null)
    {
        var query = RepositoryDbSet.AsQueryable();
        
        // User-level IDOR: filter by AppUserId if entity implements IAppUserId
        if (!appUserId.Equals(default) && typeof(IAppUserId<TKey>).IsAssignableFrom(typeof(TDomainEntity)))
        {
            query = query.Where(e => ((IAppUserId<TKey>)e).AppUserId.Equals(appUserId));
        }
        
        // Company-level IDOR: filter by CompanyId if entity implements ICompanyEntity
        if (companyId.HasValue && typeof(ICompanyEntity).IsAssignableFrom(typeof(TDomainEntity)))
        {
            query = query.Where(e => ((ICompanyEntity)e).CompanyId == companyId.Value);
        }

        return query;
    }
    
    public async Task<IEnumerable<TDALEntity>> AllAsync(TKey appUserId = default!, Guid? companyId = null)
    {
        var query = GetFilteredQuery(appUserId, companyId);

        var domainRes = await query.ToListAsync();
        
        // map domain entity => DAL entity
        var res = domainRes.Select(e => Mapper.Map(e)!);
        return res;
    }

    public async Task<TDALEntity?> FindAsync(TKey id, TKey appUserId = default!, Guid? companyId = null)
    {
        var query = GetFilteredQuery(appUserId, companyId)
            .Where(e => e.Id.Equals(id));

        var res = await query.FirstOrDefaultAsync();
        return Mapper.Map(res);
    }

    public void Add(TDALEntity entity)
    {
        RepositoryDbSet.Add(Mapper.Map(entity)!);
    }

    /// <summary>
    /// Updates an entity after verifying ownership via IDOR filtering.
    /// Returns null if the entity does not exist or fails ownership check.
    /// </summary>
    public async Task<TDALEntity?> UpdateAsync(TDALEntity entity, TKey appUserId = default!, Guid? companyId = null)
    {
        // Verify ownership: entity must pass the same IDOR filters used for reads
        var exists = await GetFilteredQuery(appUserId, companyId)
            .AnyAsync(e => e.Id.Equals(entity.Id));

        if (!exists)
        {
            return default;
        }

        // DALEntity=>DomainEntity - update - DomainEntity=>DALEntity
        return Mapper.Map(
            RepositoryDbSet.Update(
                Mapper.Map(entity)!
                ).Entity
            )!;
    }

    public void Remove(TDALEntity entity)
    {
        RepositoryDbSet.Remove(Mapper.Map(entity)!);
    }

    public async Task RemoveAsync(TKey id, TKey appUserId = default!, Guid? companyId = null)
    {
        var entity = await FindAsync(id, appUserId, companyId);
        if (entity != null)
            Remove(entity);
    }
}
