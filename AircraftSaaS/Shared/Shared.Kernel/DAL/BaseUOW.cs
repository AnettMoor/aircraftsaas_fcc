using Microsoft.EntityFrameworkCore;

namespace Shared.Kernel.DAL;

public class BaseUOW<TDbContext> : IBaseUOW
    where TDbContext : DbContext
{
    protected readonly TDbContext UowDbContext;

    public BaseUOW(TDbContext dbContext)
    {
        UowDbContext = dbContext;
    }
    
    
    public async Task<int> SaveChangesAsync()
    {
        return await UowDbContext.SaveChangesAsync();
    }
}
