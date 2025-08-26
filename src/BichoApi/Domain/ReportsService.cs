using Microsoft.EntityFrameworkCore;

public class ReportsService
{
    private readonly AppDbContext _db;
    public ReportsService(AppDbContext db) => _db = db;

    public async Task<long> TotalStakeAsync(DateTime from, DateTime to)
        => await _db.Bets.Where(b => b.CreatedAt >= from && b.CreatedAt < to)
            .SumAsync(b => (long?)b.StakeCents) ?? 0L;

    public async Task<(long stakes, long payouts)> PayoutRatioAsync(DateTime from, DateTime to)
    {
        var stakes = await _db.Bets.Where(b => b.CreatedAt >= from && b.CreatedAt < to)
            .SumAsync(b => (long?)b.StakeCents) ?? 0L;
        var payouts = await _db.BetResults.Where(r => r.CreatedAt >= from && r.CreatedAt < to && r.Matched)
            .SumAsync(r => (long?)r.GrossPayoutCents) ?? 0L;
        return (stakes, payouts);
    }
}