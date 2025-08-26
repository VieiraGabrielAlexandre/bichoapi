using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public sealed class BetEngine {
    private readonly GroupTable _groups;
    private readonly AppDbContext _db;
    public BetEngine(GroupTable groups, AppDbContext db) { _groups = groups; _db = db; }

    public async Task EvaluateDrawAsync(long drawId)
    {
        var draw = await _db.Draws.FindAsync(drawId) ?? throw new Exception("Draw não encontrado");
        var bets = await _db.Bets.Where(b => b.Status == "PENDING").ToListAsync();

        foreach (var b in bets) {
            var result = EvaluateOne(b, draw);
            _db.BetResults.Add(result);
            b.Status = result.Matched ? "WON" : "LOST";
            b.EvaluationId = result.Id;
            if (result.Matched && result.GrossPayoutCents > 0)
                await CreditAsync(b.UserId, result.GrossPayoutCents, new { betId=b.Id, drawId=draw.Id });
        }
        await _db.SaveChangesAsync();
    }

    private BetResult EvaluateOne(Bet bet, Draw draw)
    {
        var positions = bet.PositionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => Enum.Parse<PositionPick>(s)).ToList();
        var prizeIdx = BetHelpers.PrizeIndexes(Enum.Parse<PrizeWindow>(bet.PrizeWindow));
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(bet.PayloadJson)!;

        bool matched = false;
        long gross = 0;
        var details = new Dictionary<string, object>();

        switch (Enum.Parse<Modality>(bet.Modality))
        {
            case Modality.MILHAR:
            {
                var numbers = GetStringList(payload, "numbers").ToHashSet();
                foreach (var i in prizeIdx)
                    if (numbers.Contains(draw.Prizes[i])) { matched = true; gross = Prize(bet,"BASE");
                        details["hit"] = new { prize=i+1, milhar=draw.Prizes[i] }; break; }
                break;
            }
            case Modality.MILHAR_INV:
            {
                var permSet = new HashSet<string>();
                var number  = GetStringOrNull(payload, "number");
                var digits  = GetStringOrNull(payload, "digits");
                if (!string.IsNullOrEmpty(number))
                    foreach (var p in BetHelpers.Permutations(number!)) permSet.Add(p);
                if (!string.IsNullOrEmpty(digits))
                    foreach (var p in BetHelpers.CombinedPermutations(digits!, 4)) permSet.Add(p);

                foreach (var i in prizeIdx)
                    if (permSet.Contains(draw.Prizes[i])) { matched = true;
                        var combos = Math.Max(1, permSet.Count);
                        gross = Prize(bet,"BASE") / combos;
                        details["hit"] = new { prize=i+1, milhar=draw.Prizes[i], combos }; break; }
                break;
            }
            case Modality.CENTENA:
            case Modality.CENTENA_ESQ:
            {
                var hundreds = GetStringList(payload, "hundreds").DefaultIfEmpty(GetStringOrNull(payload,"hundred")).Where(s=>!string.IsNullOrEmpty(s)).Cast<string>().ToHashSet();
                foreach (var i in prizeIdx) {
                    var m = draw.Prizes[i];
                    foreach (var p in positions) {
                        var h = BetHelpers.Extract(m, p, "HUND");
                        if (hundreds.Contains(h)) { matched = true; gross = Prize(bet,"BASE");
                            details["hit"] = new { prize=i+1, hundred=h, pos=p.ToString() }; goto done; }
                    }
                }
                done: break;
            }
            case Modality.CENTENA_INV:
            case Modality.CENTENA_INV_ESQ:
            {
                var set = new HashSet<string>();
                var h    = GetStringOrNull(payload, "hundred");
                var digs = GetStringOrNull(payload, "digits");
                if (!string.IsNullOrEmpty(h))
                    foreach (var p in BetHelpers.Permutations(h!)) set.Add(p);
                if (!string.IsNullOrEmpty(digs))
                    foreach (var p in BetHelpers.CombinedPermutations(digs!, 3)) set.Add(p);

                foreach (var i in prizeIdx) {
                    var m = draw.Prizes[i];
                    foreach (var pos in positions) {
                        var hv = BetHelpers.Extract(m, pos, "HUND");
                        if (set.Contains(hv)) { matched = true;
                            var combos = Math.Max(1, set.Count);
                            gross = Prize(bet,"BASE") / combos;
                            details["hit"] = new { prize=i+1, hundred=hv, pos=pos.ToString(), combos }; goto done2; }
                    }
                }
                done2: break;
            }
            case Modality.CENTENA_3X:
            {
                var h = GetString(payload, "hundred");
                var inv = BetHelpers.Permutations(h);
                var p1 = draw.Prizes[0];
                if (BetHelpers.Extract(p1, PositionPick.RIGHT, "HUND") == h) { matched=true; gross=Prize(bet,"3X"); details["variant"]="1o normal"; break; }
                if (inv.Contains(BetHelpers.Extract(p1, PositionPick.RIGHT, "HUND"))) { matched=true; gross=Prize(bet,"3X"); details["variant"]="1o invertido"; break; }
                foreach (var i in BetHelpers.PrizeIndexes(PrizeWindow._1_5))
                    if (BetHelpers.Extract(draw.Prizes[i], PositionPick.RIGHT, "HUND") == h)
                    { matched=true; gross=Prize(bet,"3X"); details["variant"]="1/5 normal"; details["prize"]=i+1; break; }
                break;
            }
            case Modality.DEZENA:
            case Modality.DEZENA_ESQ:
            case Modality.DEZENA_MEIO:
            {
                var dzs = GetStringList(payload, "dozens").ToHashSet();
                foreach (var i in prizeIdx) {
                    foreach (var p in positions) {
                        var dz = BetHelpers.Extract(draw.Prizes[i], p, "DOZEN");
                        if (dzs.Contains(dz)) { matched=true; gross=Prize(bet,"BASE");
                            details["dz"]=dz; details["pos"]=p.ToString(); details["prize"]=i+1; goto doneDz; }
                    }
                }
                doneDz: break;
            }
            case Modality.DUQUE_DE_DEZENA:
            {
                var dzs = GetStringList(payload, "dozens").Distinct().ToList();
                var pool = new HashSet<string>();
                foreach (var i in prizeIdx) foreach (var p in positions) pool.Add(BetHelpers.Extract(draw.Prizes[i], p, "DOZEN"));
                var hits = dzs.Count(d => pool.Contains(d));
                if (hits >= 2) { matched = true; var combos = dzs.Count < 2 ? 1 : BetHelpers.Comb(dzs.Count,2);
                    gross = Prize(bet,"BASE") * hits / Math.Max(1, combos);
                    details["hits"]=hits; details["combos"]=combos; }
                break;
            }
            case Modality.GRUPO:
            case Modality.GRUPO_ESQ:
            case Modality.GRUPO_MEIO:
            {
                var gps = GetIntList(payload, "groups").ToHashSet();
                foreach (var i in prizeIdx) {
                    foreach (var p in positions) {
                        var dz = BetHelpers.Extract(draw.Prizes[i], p, "DOZEN");
                        var gp = _groups.GroupOfDozen(dz);
                        if (gps.Contains(gp)) { matched=true; gross=Prize(bet,"BASE");
                            details["group"]=gp; details["dz"]=dz; details["pos"]=p.ToString(); details["prize"]=i+1; goto doneGp; }
                    }
                }
                doneGp: break;
            }
            // As demais modalidades seguem o mesmo padrão (TERNO/QUADRA/QUINA/SENA, PALPITÃO, etc.)
            default: break;
        }

        return new BetResult {
            BetId = bet.Id, DrawId = draw.Id, Matched = matched,
            GrossPayoutCents = matched ? gross : 0,
            DetailsJson = JsonSerializer.Serialize(details)
        };
    }

    private long Prize(Bet bet, string key)
    {
        var rec = _db.PayoutTables.FirstOrDefault(p => p.Modality == bet.Modality && p.Key == key)
               ?? _db.PayoutTables.FirstOrDefault(p => p.Modality == bet.Modality && p.Key == "BASE");
        if (rec is null) return 0;
        return (bet.StakeCents * rec.MultiplierBp) / 10000L;
    }

    private async Task CreditAsync(long userId, long amount, object meta)
    {
        var w = await _db.Wallets.FirstAsync(x => x.UserId == userId);
        w.BalanceCents += amount;
        _db.WalletTransactions.Add(new WalletTransaction{
            WalletId = w.Id, Type = "ADJUST", AmountCents = amount,
            MetaJson = JsonSerializer.Serialize(meta)
        });
    }

    // payload helpers
    private static List<string> GetStringList(Dictionary<string,object> p, string key)
      => p.TryGetValue(key, out var el) && el is System.Text.Json.JsonElement je && je.ValueKind==JsonValueKind.Array
        ? je.EnumerateArray().Select(x=>x.GetString()!).ToList()
        : new List<string>();
    private static List<int> GetIntList(Dictionary<string,object> p, string key)
      => p.TryGetValue(key, out var el) && el is System.Text.Json.JsonElement je && je.ValueKind==JsonValueKind.Array
        ? je.EnumerateArray().Select(x=>x.GetInt32()).ToList()
        : new List<int>();
    private static string GetString(Dictionary<string,object> p, string key)
      => ((System.Text.Json.JsonElement)p[key]).GetString()!;
    private static string? GetStringOrNull(Dictionary<string,object> p, string key)
      => p.TryGetValue(key, out var el) ? ((System.Text.Json.JsonElement)el).GetString() : null;
}
