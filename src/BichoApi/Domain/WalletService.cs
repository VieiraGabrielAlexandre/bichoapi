using Microsoft.EntityFrameworkCore;

public class WalletService
{
    private readonly AppDbContext _db;
    public WalletService(AppDbContext db) => _db = db;

    public async Task<long> GetBalanceAsync(long userId)
        => await _db.Wallets.Where(w => w.UserId == userId).Select(w => w.BalanceCents).SingleAsync();

    public async Task RechargeAsync(long userId, long amountCents, object? meta = null)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        var w = await _db.Wallets.SingleAsync(x => x.UserId == userId);
        w.BalanceCents += amountCents;
        w.UpdatedAt = DateTime.UtcNow;
        _db.WalletTransactions.Add(new WalletTransaction {
            WalletId = w.Id, Type="RECHARGE", AmountCents = amountCents,
            MetaJson = meta is null ? null : System.Text.Json.JsonSerializer.Serialize(meta)
        });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<bool> TryDebitAsync(long userId, long amountCents, object? meta = null)
    {
        using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var w = await _db.Wallets.SingleAsync(x => x.UserId == userId);
        if (w.BalanceCents < amountCents) return false;
        w.BalanceCents -= amountCents;
        w.UpdatedAt = DateTime.UtcNow;
        _db.WalletTransactions.Add(new WalletTransaction {
            WalletId = w.Id, Type="DEBIT", AmountCents = amountCents,
            MetaJson = meta is null ? null : System.Text.Json.JsonSerializer.Serialize(meta)
        });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }
}