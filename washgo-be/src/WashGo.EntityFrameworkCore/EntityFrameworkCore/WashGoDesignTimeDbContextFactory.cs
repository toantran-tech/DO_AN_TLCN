using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WashGo.EntityFrameworkCore.EntityFrameworkCore;

/// <summary>Schema generation only. Does not read workstation secrets or connect to an application database.</summary>
public class WashGoDesignTimeDbContextFactory : IDesignTimeDbContextFactory<WashGoDbContext>
{
    public WashGoDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<WashGoDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=design_only;Username=design_only;Password=not-a-secret;Timeout=1")
            .Options);
}
