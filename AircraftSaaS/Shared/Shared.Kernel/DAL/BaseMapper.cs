namespace Shared.Kernel.DAL;

/// <summary>
/// Identity mapper — passes entities through unchanged.
/// Used when DAL entities are the same as domain entities (no separate DAL DTO layer).
/// </summary>
public class BaseMapper<TEntity> : IBaseMapper<TEntity, TEntity>
    where TEntity : class
{
    public TEntity? Map(TEntity? entity) => entity;
}
