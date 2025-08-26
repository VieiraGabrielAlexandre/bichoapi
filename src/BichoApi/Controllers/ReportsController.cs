using Microsoft.AspNetCore.Mvc;

[ApiController, Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportsService _reports;
    public ReportsController(ReportsService reports) => _reports = reports;

    [HttpGet("payout-ratio")]
    public async Task<IActionResult> PayoutRatio([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var (stakes, payouts) = await _reports.PayoutRatioAsync(from, to);
        return Ok(new { stakes, payouts, ratio = stakes == 0 ? 0 : (double)payouts / stakes });
    }

    [HttpGet("total-stake")]
    public async Task<IActionResult> TotalStake([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(new { total = await _reports.TotalStakeAsync(from, to) });
}