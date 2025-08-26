using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController, Route("api/draws")]
public class DrawsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BetEngine _engine;
    public DrawsController(AppDbContext db, BetEngine engine) { _db = db; _engine = engine; }

    public sealed class CreateDrawRequest {
        public string Market { get; set; } = "PTM";
        public DateOnly DrawDate { get; set; }
        public TimeOnly DrawTime { get; set; }
        public List<string> Prizes { get; set; } = new();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDrawRequest req)
    {
        if (req.Prizes.Count < 6) return BadRequest("mínimo 6 prêmios (1/6)");
        if (req.Prizes.Any(p => p.Length!=4 || !p.All(char.IsDigit)))
            return BadRequest("prêmios devem ser milhares de 4 dígitos");

        var d = new Draw {
            Market = req.Market,
            DrawDate = req.DrawDate,
            DrawTime = req.DrawTime,
            Prizes = req.Prizes
        };
        _db.Draws.Add(d);
        await _db.SaveChangesAsync();
        return Ok(new { d.Id });
    }

    [HttpPost("{drawId:long}/evaluate")]
    public async Task<IActionResult> Evaluate(long drawId)
    {
        await _engine.EvaluateDrawAsync(drawId);
        var results = _db.BetResults.Where(r=>r.DrawId==drawId);
        return Ok(new { count = await results.CountAsync() });
    }
}