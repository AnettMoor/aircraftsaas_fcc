namespace Base.DAL.Contracts;

public interface IBaseUOW
{
    Task<int> SaveChangesAsync();

}