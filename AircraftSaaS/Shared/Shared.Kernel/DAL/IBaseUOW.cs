namespace Shared.Kernel.DAL;

public interface IBaseUOW
{
    Task<int> SaveChangesAsync();
}
