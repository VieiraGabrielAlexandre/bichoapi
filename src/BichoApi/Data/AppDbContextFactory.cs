using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional:true)
            .AddEnvironmentVariables()
            .Build();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cfg.GetConnectionString("Db")!)
            .Options;
        return new AppDbContext(options);
    }
}