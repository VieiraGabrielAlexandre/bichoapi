using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users"); b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Email).HasMaxLength(160);
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasOne(x => x.Wallet).WithOne(w => w.User).HasForeignKey<Wallet>(w => w.UserId);
    }
}

public class WalletConfig : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> b)
    {
        b.ToTable("wallets"); b.HasKey(x => x.Id);
        b.Property(x => x.BalanceCents).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
    }
}

public class WalletTransactionConfig : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> b)
    {
        b.ToTable("wallet_transactions"); b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(20).IsRequired();
        b.Property(x => x.AmountCents).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasOne(x => x.Wallet).WithMany(w => w.Transactions).HasForeignKey(x => x.WalletId);
    }
}

public class DrawConfig : IEntityTypeConfiguration<Draw>
{
    public void Configure(EntityTypeBuilder<Draw> b)
    {
        b.ToTable("draws"); b.HasKey(x => x.Id);
        b.Property(x => x.Market).HasMaxLength(40).IsRequired();
        b.Property(x => x.DrawDate).IsRequired();
        b.Property(x => x.DrawTime).IsRequired();
        b.Property(x => x.Prizes).HasColumnType("text[]"); // Postgres; para MySQL use conversor JSON
        b.HasIndex(x => new { x.Market, x.DrawDate, x.DrawTime }).IsUnique();
    }
}

public class BetConfig : IEntityTypeConfiguration<Bet>
{
    public void Configure(EntityTypeBuilder<Bet> b)
    {
        b.ToTable("bets"); b.HasKey(x => x.Id);
        b.Property(x => x.Modality).HasMaxLength(40).IsRequired();
        b.Property(x => x.PositionsCsv).HasMaxLength(120).IsRequired();
        b.Property(x => x.PrizeWindow).HasMaxLength(10).IsRequired();
        b.Property(x => x.StakeCents).IsRequired();
        b.Property(x => x.PayloadJson).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasOne(x => x.User).WithMany(u => u.Bets).HasForeignKey(x => x.UserId);
    }
}

public class BetResultConfig : IEntityTypeConfiguration<BetResult>
{
    public void Configure(EntityTypeBuilder<BetResult> b)
    {
        b.ToTable("bet_results"); b.HasKey(x => x.Id);
        b.Property(x => x.Matched).IsRequired();
        b.Property(x => x.GrossPayoutCents).IsRequired();
        b.HasOne(x => x.Bet).WithMany(bet => bet.Results).HasForeignKey(x => x.BetId);
        b.HasOne(x => x.Draw).WithMany(d => d.Results).HasForeignKey(x => x.DrawId);
    }
}

public class PayoutTableConfig : IEntityTypeConfiguration<PayoutTable>
{
    public void Configure(EntityTypeBuilder<PayoutTable> b)
    {
        b.ToTable("payout_tables"); b.HasKey(x => x.Id);
        b.Property(x => x.Modality).HasMaxLength(40).IsRequired();
        b.Property(x => x.Key).HasMaxLength(60).IsRequired();
        b.Property(x => x.MultiplierBp).IsRequired();
        b.HasIndex(x => new { x.Modality, x.Key }).IsUnique();
    }
}
