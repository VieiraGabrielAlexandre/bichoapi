using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public sealed class BetEngine
{
    private readonly GroupTable _groups;
    private readonly AppDbContext _db;
    public BetEngine(GroupTable groups, AppDbContext db) { _groups = groups; _db = db; }

    public async Task EvaluateDrawAsync(long drawId)
    {
        var draw = await _db.Draws.FindAsync(drawId) ?? throw new Exception("Draw não encontrado");
        var bets = await _db.Bets.Where(b => b.Status == "PENDING").ToListAsync();

        foreach (var b in bets)
        {
            var result = EvaluateOne(b, draw);
            _db.BetResults.Add(result);
            b.Status = result.Matched ? "WON" : "LOST";
            b.EvaluationId = result.Id;

            if (result.Matched && result.GrossPayoutCents > 0)
                await CreditAsync(b.UserId, result.GrossPayoutCents, new { betId = b.Id, drawId = draw.Id });
        }

        await _db.SaveChangesAsync();
    }

    private BetResult EvaluateOne(Bet bet, Draw draw)
    {
        var positions = bet.PositionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => Enum.Parse<PositionPick>(s)).DefaultIfEmpty(PositionPick.RIGHT).ToList();
        var prizeIdx = BetHelpers.PrizeIndexes(Enum.Parse<PrizeWindow>(bet.PrizeWindow));
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(bet.PayloadJson)!;

        bool matched = false;
        long gross = 0;
        var details = new Dictionary<string, object>();

        switch (Enum.Parse<Modality>(bet.Modality))
        {
            // -------------------- MILHAR (simples) --------------------
            case Modality.MILHAR:
            {
                var numbers = GetStringList(payload, "numbers").ToHashSet();
                foreach (var i in prizeIdx)
                    if (numbers.Contains(draw.Prizes[i]))
                    { matched = true; gross = Prize(bet, "BASE");
                      details["hit"] = new { prize = i + 1, milhar = draw.Prizes[i] }; break; }
                break;
            }

            // -------------------- MILHAR INV (permuta/combinada) --------------------
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
                    if (permSet.Contains(draw.Prizes[i]))
                    { matched = true; var combos = Math.Max(1, permSet.Count);
                      gross = Prize(bet, "BASE") / combos;
                      details["hit"] = new { prize = i + 1, milhar = draw.Prizes[i], combos }; break; }
                break;
            }

            // -------------------- MILHAR E CT (atalho: milhar + centena = metade) --------------------
            case Modality.MILHAR_E_CT:
            {
                var number = GetString(payload, "number"); // ex: "7517"
                long total = 0;
                // (1) MILHAR (usa key "MILHAR")
                foreach (var i in prizeIdx)
                {
                    if (draw.Prizes[i] == number)
                    { total += Prize(bet, "MILHAR"); details["milharPrize"] = i + 1; break; }
                }
                // (2) CENTENA (usa key "CENTENA") sobre posições selecionadas
                foreach (var i in prizeIdx)
                {
                    foreach (var pos in positions)
                    {
                        var hv = BetHelpers.Extract(draw.Prizes[i], pos, "HUND");
                        if (hv == number.Substring(1, 3)) // 3 últimos da direita do seu milhar
                        { total += Prize(bet, "CENTENA"); details["centenaPrize"] = i + 1; details["pos"] = pos.ToString(); goto _end; }
                    }
                }
            _end:
                if (total > 0) { matched = true; gross = total; }
                break;
            }

            // -------------------- CENTENA (direita) e CENTENA_ESQ --------------------
            case Modality.CENTENA:
            case Modality.CENTENA_ESQ:
            {
                var hundreds = GetStringList(payload, "hundreds")
                                .DefaultIfEmpty(GetStringOrNull(payload, "hundred"))
                                .Where(s => !string.IsNullOrEmpty(s))
                                .Cast<string>()
                                .ToHashSet();

                foreach (var i in prizeIdx)
                {
                    var m = draw.Prizes[i];
                    foreach (var p in positions)
                    {
                        var h = BetHelpers.Extract(m, p, "HUND");
                        if (hundreds.Contains(h))
                        { matched = true; gross = Prize(bet, "BASE");
                          details["hit"] = new { prize = i + 1, hundred = h, pos = p.ToString() }; goto doneCent; }
                    }
                }
            doneCent: break;
            }

            // -------------------- CENTENA INV e CENTENA INV ESQ --------------------
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

                foreach (var i in prizeIdx)
                {
                    var m = draw.Prizes[i];
                    foreach (var pos in positions)
                    {
                        var hv = BetHelpers.Extract(m, pos, "HUND");
                        if (set.Contains(hv))
                        { matched = true; var combos = Math.Max(1, set.Count);
                          gross = Prize(bet, "BASE") / combos;
                          details["hit"] = new { prize = i + 1, hundred = hv, pos = pos.ToString(), combos }; goto doneCin; }
                    }
                }
            doneCin: break;
            }

            // -------------------- CENTENA 3X --------------------
            case Modality.CENTENA_3X:
            {
                var h = GetString(payload, "hundred");
                var inv = BetHelpers.Permutations(h);
                var p1 = draw.Prizes[0];

                // (1) 1º prêmio normal
                if (BetHelpers.Extract(p1, PositionPick.RIGHT, "HUND") == h)
                { matched = true; gross = Prize(bet, "3X"); details["variant"] = "1o normal"; break; }

                // (2) 1º prêmio invertido
                if (inv.Contains(BetHelpers.Extract(p1, PositionPick.RIGHT, "HUND")))
                { matched = true; gross = Prize(bet, "3X"); details["variant"] = "1o invertido"; break; }

                // (3) 1/5 normal
                foreach (var i in BetHelpers.PrizeIndexes(PrizeWindow._1_5))
                    if (BetHelpers.Extract(draw.Prizes[i], PositionPick.RIGHT, "HUND") == h)
                    { matched = true; gross = Prize(bet, "3X"); details["variant"] = "1/5 normal"; details["prize"] = i + 1; break; }
                break;
            }

            // -------------------- UNIDADE --------------------
            case Modality.UNIDADE:
            {
                // aceita "digits": ["0","7"] ou "digit": "7"
                var digs = GetStringList(payload, "digits")
                           .DefaultIfEmpty(GetStringOrNull(payload, "digit"))
                           .Where(s => !string.IsNullOrEmpty(s))
                           .Cast<string>()
                           .ToHashSet();
                foreach (var i in prizeIdx)
                {
                    foreach (var p in positions)
                    {
                        var u = BetHelpers.Extract(draw.Prizes[i], p, "UNIT");
                        if (digs.Contains(u))
                        { matched = true; gross = Prize(bet, "BASE");
                          details["unit"] = u; details["pos"] = p.ToString(); details["prize"] = i + 1; goto doneU; }
                    }
                }
            doneU: break;
            }

            // -------------------- DEZENA / ESQ / MEIO --------------------
            case Modality.DEZENA:
            case Modality.DEZENA_ESQ:
            case Modality.DEZENA_MEIO:
            {
                var dzs = GetStringList(payload, "dozens").ToHashSet();
                foreach (var i in prizeIdx)
                {
                    foreach (var p in positions)
                    {
                        var dz = BetHelpers.Extract(draw.Prizes[i], p, "DOZEN");
                        if (dzs.Contains(dz))
                        { matched = true; gross = Prize(bet, "BASE");
                          details["dz"] = dz; details["pos"] = p.ToString(); details["prize"] = i + 1; goto doneDz; }
                    }
                }
            doneDz: break;
            }

            // -------------------- DUQUE DE DEZENA (inclui ESQ e MEIO via positions) --------------------
            case Modality.DUQUE_DE_DEZENA:
            case Modality.DUQUE_DEZENA_ESQ:
            case Modality.DUQUE_DEZENA_MEIO:
            {
                var dzs = GetStringList(payload, "dozens").Distinct().ToList();
                var pool = CollectDozens(draw, prizeIdx, positions);
                var hits = dzs.Count(pool.Contains);
                if (hits >= 2)
                {
                    matched = true;
                    var combos = dzs.Count < 2 ? 1 : BetHelpers.Comb(dzs.Count, 2);
                    // Paga proporcional às combinações possíveis que você comprou
                    // (se quiser pagar por número de pares vencedores específicos, substitua por Comb(hits,2))
                    gross = Prize(bet, "BASE") * hits / Math.Max(1, combos);
                    details["hits"] = hits; details["combos"] = combos;
                }
                break;
            }

            // -------------------- TERNO DZ SECO (1/3 em qualquer ordem) --------------------
            case Modality.TERNO_DZ_SECO:
            case Modality.TERNO_DZ_SECO_ESQ:
            {
                var dzs = GetStringList(payload, "dozens").Distinct().ToList();
                // precisa exatamente 3 dezenas
                var pool = CollectDozens(draw, BetHelpers.PrizeIndexes(PrizeWindow._1_3), positions);
                var allIn = dzs.All(pool.Contains) && dzs.Count == 3;
                if (allIn) { matched = true; gross = Prize(bet, "SECO"); details["hits"] = 3; }
                break;
            }

            // -------------------- TERNO DZ (combinado / 1/5) --------------------
            case Modality.TERNO_DZ:
            {
                var dzs = GetStringList(payload, "dozens").Distinct().ToList();
                var pool = CollectDozens(draw, BetHelpers.PrizeIndexes(PrizeWindow._1_5), positions);
                var hits = dzs.Count(pool.Contains);
                if (hits >= 3)
                {
                    matched = true;
                    var combos = dzs.Count < 3 ? 1 : BetHelpers.Comb(dzs.Count, 3);
                    var winners = BetHelpers.Comb(hits, 3);
                    gross = (Prize(bet, "BASE") * winners) / Math.Max(1, combos);
                    details["hits"] = hits; details["winners"] = winners; details["combos"] = combos;
                }
                break;
            }

            // -------------------- GRUPO (e ESQ/MEIO via positions) --------------------
            case Modality.GRUPO:
            case Modality.GRUPO_ESQ:
            case Modality.GRUPO_MEIO:
            {
                var gps = GetIntList(payload, "groups").ToHashSet();
                foreach (var i in prizeIdx)
                {
                    foreach (var p in positions)
                    {
                        var dz = BetHelpers.Extract(draw.Prizes[i], p, "DOZEN");
                        var gp = _groups.GroupOfDozen(dz);
                        if (gps.Contains(gp))
                        { matched = true; gross = Prize(bet, "BASE");
                          details["group"] = gp; details["dz"] = dz; details["pos"] = p.ToString(); details["prize"] = i + 1; goto doneGp; }
                    }
                }
            doneGp: break;
            }

            // -------------------- DUQUE DE GRUPO (inclui ESQ/MEIO via positions) --------------------
            case Modality.DUQUE_DE_GRUPO:
            case Modality.DUQUE_DE_GRUPO_ESQ:
            case Modality.DUQUE_DE_GRUPO_MEIO:
            {
                var gps = GetIntList(payload, "groups").Distinct().ToList();
                var pool = CollectGroups(draw, prizeIdx, positions);
                var hits = gps.Count(pool.Contains);
                if (hits >= 2)
                {
                    matched = true;
                    var combos = gps.Count < 2 ? 1 : BetHelpers.Comb(gps.Count, 2);
                    gross = Prize(bet, "BASE") * hits / Math.Max(1, combos);
                    details["hits"] = hits; details["combos"] = combos;
                }
                break;
            }

            // -------------------- TERNO/QUADRA/QUINA/SENA DE GRUPO (+ ESQ/MEIO) --------------------
            case Modality.TERNO_GP:
            case Modality.TERNO_GP_ESQ:
            case Modality.TERNO_GP_MEIO:
            {
                matched = KofN_GroupCombo(bet, draw, prizeIdx, positions, k: 3, key: "BASE", detailsOut: details, out gross);
                break;
            }
            case Modality.QUADRA_GP:
            case Modality.QUADRA_GP_ESQ:
            case Modality.QUADRA_GP_MEIO:
            {
                // regra: joga com 4 grupos; ganha acertando os 4 do 1/5
                var gps = GetIntList(payload, "groups").Distinct().ToList();
                var pool = CollectGroups(draw, BetHelpers.PrizeIndexes(PrizeWindow._1_5), positions);
                var all = gps.All(pool.Contains) && gps.Count == 4;
                if (all) { matched = true; gross = Prize(bet, "BASE"); details["hits"] = 4; }
                break;
            }
            case Modality.QUINA_GP:
            case Modality.QUINA_GP_ESQ:
            case Modality.QUINA_GP_MEIO:
            {
                matched = KofN_GroupCombo(bet, draw, BetHelpers.PrizeIndexes(PrizeWindow._1_5), positions, k: 5, key: "BASE", detailsOut: details, out gross);
                break;
            }
            case Modality.SENA_GP:
            case Modality.SENA_GP_ESQ:
            case Modality.SENA_GP_MEIO:
            {
                matched = KofN_GroupCombo(bet, draw, BetHelpers.PrizeIndexes(PrizeWindow._1_6), positions, k: 6, key: "BASE", detailsOut: details, out gross);
                break;
            }

            // -------------------- PALPITÃO (20 dezenas; paga 1%,10%,100% para Terno/Quadra/Quina no 1/5) --------------------
            case Modality.PALPITAO:
            {
                var dzs = GetStringList(payload, "dozens").Distinct().ToList(); // até 20
                var pool = CollectDozens(draw, BetHelpers.PrizeIndexes(PrizeWindow._1_5), new List<PositionPick>{ PositionPick.RIGHT });
                var hits = dzs.Count(pool.Contains);
                long total = 0;

                if (hits >= 3)
                {
                    var w3 = BetHelpers.Comb(hits, 3);
                    // chaves de payout específicas: TERNO (1%), QUADRA (10%), QUINA (100%)
                    total += w3 * Prize(bet, "TERNO");
                }
                if (hits >= 4)
                {
                    var w4 = BetHelpers.Comb(hits, 4);
                    total += w4 * Prize(bet, "QUADRA");
                }
                if (hits >= 5)
                {
                    var w5 = BetHelpers.Comb(hits, 5);
                    total += w5 * Prize(bet, "QUINA");
                }

                if (total > 0) { matched = true; gross = total; details["hits"] = hits; }
                break;
            }

            // -------------------- SENINHA / QUININHA / LOTINHA --------------------
            case Modality.SENINHA:
            {
                matched = LotGame(bet, draw, prizeIdx: BetHelpers.PrizeIndexes(PrizeWindow._1_6),
                                  chosen: GetStringList(payload, "dozens").Distinct().ToList(),
                                  keysByHits: new Dictionary<int, string>{{6,"6"},{5,"5"},{4,"4"}},
                                  detailsOut: details, out gross);
                break;
            }
            case Modality.QUININHA:
            {
                matched = LotGame(bet, draw, prizeIdx: BetHelpers.PrizeIndexes(PrizeWindow._1_6),
                                  chosen: GetStringList(payload, "dozens").Distinct().ToList(),
                                  keysByHits: new Dictionary<int, string>{{5,"5"},{4,"4"},{3,"3"}},
                                  detailsOut: details, out gross);
                break;
            }
            case Modality.LOTINHA:
            {
                matched = LotGame(bet, draw, prizeIdx: BetHelpers.PrizeIndexes(PrizeWindow._1_6),
                                  chosen: GetStringList(payload, "dozens").Distinct().ToList(),
                                  keysByHits: new Dictionary<int, string>{{15,"15"}},
                                  detailsOut: details, out gross);
                break;
            }

            // -------------------- PASSE VAI / PASSE VAI E VEM --------------------
            case Modality.PASSE_VAI:
            {
                // payload { "groupsOrdered": [g1, g2] }
                var arr = GetIntList(payload, "groupsOrdered");
                if (arr.Count == 2)
                {
                    var g1 = arr[0]; var g2 = arr[1];
                    bool ok1 = false, ok2 = false;

                    // g1 deve estar no 1º prêmio (em alguma das posições escolhidas)
                    foreach (var pos in positions)
                    {
                        var dz = BetHelpers.Extract(draw.Prizes[0], pos, "DOZEN");
                        if (_groups.GroupOfDozen(dz) == g1) { ok1 = true; break; }
                    }

                    // g2 deve estar em qualquer dos demais prêmios da janela (exceto 1º)
                    foreach (var i in prizeIdx.Where(x => x != 0))
                    {
                        foreach (var pos in positions)
                        {
                            var dz = BetHelpers.Extract(draw.Prizes[i], pos, "DOZEN");
                            if (_groups.GroupOfDozen(dz) == g2) { ok2 = true; break; }
                        }
                        if (ok2) break;
                    }

                    if (ok1 && ok2) { matched = true; gross = Prize(bet, "BASE"); details["g1"] = g1; details["g2"] = g2; }
                }
                break;
            }

            case Modality.PASSE_VAI_E_VEM:
            {
                // payload { "groups": [g1, g2] } (ordem não importa)
                var arr = GetIntList(payload, "groups").Distinct().ToList();
                if (arr.Count == 2)
                {
                    var gA = arr[0]; var gB = arr[1];
                    bool variant1 = false, variant2 = false;

                    // Variante 1: gA no 1º, gB em qualquer outro
                    foreach (var pos in positions)
                    {
                        var dz = BetHelpers.Extract(draw.Prizes[0], pos, "DOZEN");
                        if (_groups.GroupOfDozen(dz) == gA)
                        {
                            foreach (var i in prizeIdx.Where(x => x != 0))
                            {
                                foreach (var p in positions)
                                {
                                    var dz2 = BetHelpers.Extract(draw.Prizes[i], p, "DOZEN");
                                    if (_groups.GroupOfDozen(dz2) == gB) { variant1 = true; goto doneA; }
                                }
                            }
                        }
                    }
                doneA: ;

                    // Variante 2: gB no 1º, gA em qualquer outro
                    foreach (var pos in positions)
                    {
                        var dz = BetHelpers.Extract(draw.Prizes[0], pos, "DOZEN");
                        if (_groups.GroupOfDozen(dz) == gB)
                        {
                            foreach (var i in prizeIdx.Where(x => x != 0))
                            {
                                foreach (var p in positions)
                                {
                                    var dz2 = BetHelpers.Extract(draw.Prizes[i], p, "DOZEN");
                                    if (_groups.GroupOfDozen(dz2) == gA) { variant2 = true; goto doneB; }
                                }
                            }
                        }
                    }
                doneB: ;

                    if (variant1 || variant2) { matched = true; gross = Prize(bet, "BASE"); details["groups"] = arr; }
                }
                break;
            }

            // -------------------- default --------------------
            default:
                // modalidade não implementada (deixar sem prêmio)
                break;
        }

        return new BetResult
        {
            BetId = bet.Id,
            DrawId = draw.Id,
            Matched = matched,
            GrossPayoutCents = matched ? gross : 0,
            DetailsJson = JsonSerializer.Serialize(details)
        };
    }

    // ======================= Helpers de cálculo =======================

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
        _db.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = w.Id,
            Type = "ADJUST",
            AmountCents = amount,
            MetaJson = JsonSerializer.Serialize(meta)
        });
    }

    private static List<string> GetStringList(Dictionary<string, object> p, string key)
      => p.TryGetValue(key, out var el) && el is JsonElement je && je.ValueKind == JsonValueKind.Array
        ? je.EnumerateArray().Select(x => x.GetString()!).Where(s => s != null).ToList() : new();

    private static List<int> GetIntList(Dictionary<string, object> p, string key)
      => p.TryGetValue(key, out var el) && el is JsonElement je && je.ValueKind == JsonValueKind.Array
        ? je.EnumerateArray().Select(x => x.GetInt32()).ToList() : new();

    private static string GetString(Dictionary<string, object> p, string key)
      => ((JsonElement)p[key]).GetString()!;

    private static string? GetStringOrNull(Dictionary<string, object> p, string key)
      => p.TryGetValue(key, out var el) ? ((JsonElement)el).GetString() : null;

    private static HashSet<string> CollectDozens(Draw draw, IEnumerable<int> prizeIdx, IEnumerable<PositionPick> positions)
    {
        var pool = new HashSet<string>();
        foreach (var i in prizeIdx)
            foreach (var p in positions)
                pool.Add(BetHelpers.Extract(draw.Prizes[i], p, "DOZEN"));
        return pool;
    }

    private HashSet<int> CollectGroups(Draw draw, IEnumerable<int> prizeIdx, IEnumerable<PositionPick> positions)
    {
        var pool = new HashSet<int>();
        foreach (var i in prizeIdx)
            foreach (var p in positions)
            {
                var dz = BetHelpers.Extract(draw.Prizes[i], p, "DOZEN");
                pool.Add(_groups.GroupOfDozen(dz));
            }
        return pool;
    }

    /// <summary>
    /// Avalia modalidades de grupo com acerto mínimo "k" e pagamento proporcional ao número de combinações que você comprou.
    /// Ex.: TERNO_GP (k=3), QUINA_GP (k=5), SENA_GP (k=6).
    /// </summary>
    private bool KofN_GroupCombo(Bet bet, Draw draw, IEnumerable<int> prizeIdx, IEnumerable<PositionPick> positions,
                                 int k, string key, Dictionary<string, object> detailsOut, out long gross)
    {
        var gps = GetIntList(JsonSerializer.Deserialize<Dictionary<string, object>>(bet.PayloadJson)!, "groups").Distinct().ToList();
        var pool = CollectGroups(draw, prizeIdx, positions);
        var hits = gps.Count(pool.Contains);
        if (hits >= k)
        {
            var combos = gps.Count < k ? 1 : BetHelpers.Comb(gps.Count, k);
            var winners = BetHelpers.Comb(hits, k);
            gross = (Prize(bet, key) * winners) / Math.Max(1, combos);
            detailsOut["hits"] = hits; detailsOut["winners"] = winners; detailsOut["combos"] = combos;
            return true;
        }
        gross = 0;
        return false;
    }

    /// <summary>
    /// Jogos "lotinha/seninha/quininha": avalia por número de acertos (1/6), com chaves de payout por quantidade.
    /// </summary>
    private bool LotGame(Bet bet, Draw draw, IEnumerable<int> prizeIdx, List<string> chosen,
                         Dictionary<int, string> keysByHits, Dictionary<string, object> detailsOut, out long gross)
    {
        var pool = CollectDozens(draw, prizeIdx, new List<PositionPick> { PositionPick.RIGHT });
        var hits = chosen.Distinct().Count(pool.Contains);
        gross = 0;

        // paga usando a maior chave atingida (ex.: 6 antes de 5/4)
        foreach (var kv in keysByHits.OrderByDescending(kv => kv.Key))
        {
            if (hits >= kv.Key) { gross = Prize(bet, kv.Value); break; }
        }
        if (gross > 0) { detailsOut["hits"] = hits; return true; }
        return false;
    }
}
