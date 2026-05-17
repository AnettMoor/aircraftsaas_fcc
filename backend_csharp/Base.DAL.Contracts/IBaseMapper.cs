namespace Base.DAL.Contracts;

public interface IBaseMapper<TEntityOut, TEntityIn>
    where TEntityIn : class
    where TEntityOut : class
{
    TEntityOut? Map(TEntityIn? entity);
    TEntityIn? Map(TEntityOut? entity);
}