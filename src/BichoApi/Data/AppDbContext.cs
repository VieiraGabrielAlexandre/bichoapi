using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Draw> Draws => Set<Draw>();
    public DbSet<Bet> Bets => Set<Bet>();
    public DbSet<BetResult> BetResults => Set<BetResult>();
    public DbSet<PayoutTable> PayoutTables => Set<PayoutTable>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfiguration(new UserConfig());
        mb.ApplyConfiguration(new WalletConfig());
        mb.ApplyConfiguration(new WalletTransactionConfig());
        mb.ApplyConfiguration(new DrawConfig());
        mb.ApplyConfiguration(new BetConfig());
        mb.ApplyConfiguration(new BetResultConfig());
        mb.ApplyConfiguration(new PayoutTableConfig());
    }
}