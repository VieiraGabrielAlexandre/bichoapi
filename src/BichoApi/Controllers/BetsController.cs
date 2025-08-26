using Microsoft.AspNetCore.Mvc;

[ApiController, Route("api/bets")]
public class BetsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WalletService _wallet;

    public BetsController(AppDbContext db, WalletService wallet) { _db = db; _wallet = wallet; }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBetRequest req)
    {
        if (req.StakeCents <= 0) return BadRequest("Valor inválido.");
        var ok = await _wallet.TryDebitAsync(req.UserId, req.StakeCents,
            new { reason="BET_STAKE", modality=req.Modality.ToString() });
        if (!ok) return BadRequest("Saldo insuficiente.");

        var bet = new Bet {
            UserId = req.UserId,
            Modality = req.Modality.ToString(),
            PositionsCsv = string.Join(',', req.Positions),
            PrizeWindow = req.PrizeWindow.ToString(),
            StakeCents = req.StakeCents,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(req.Payload)
        };
        _db.Bets.Add(bet);
        await _db.SaveChangesAsync();
        return Ok(new { bet.Id });
    }
}