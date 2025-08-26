using Microsoft.AspNetCore.Mvc;

[ApiController, Route("api/wallets")]
public class WalletsController : ControllerBase
{
    private readonly WalletService _wallet;
    public WalletsController(WalletService wallet) => _wallet = wallet;

    [HttpPost("{userId:long}/recharge")]
    public async Task<IActionResult> Recharge(long userId, [FromBody] long amountCents)
    {
        await _wallet.RechargeAsync(userId, amountCents, new { reason="RECHARGE_API" });
        var bal = await _wallet.GetBalanceAsync(userId);
        return Ok(new { balance = bal });
    }

    [HttpGet("{userId:long}/balance")]
    public async Task<IActionResult> Balance(long userId)
        => Ok(new { balance = await _wallet.GetBalanceAsync(userId) });
}