using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fleet.Infrastructure;

internal class FleetDbContextFactory : IDesignTimeDbContextFactory<FleetDbContext>
{
    public FleetDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FleetDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=aircraft-fleet;Username=postgres;Password=postgres");
        return new FleetDbContext(optionsBuilder.Options);
    }
}
