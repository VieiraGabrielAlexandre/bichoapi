using Microsoft.AspNetCore.Mvc;

[ApiController, Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User u)
    {
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        _db.Wallets.Add(new Wallet { UserId = u.Id, BalanceCents = 0 });
        await _db.SaveChangesAsync();
        return Ok(new { u.Id });
    }
}