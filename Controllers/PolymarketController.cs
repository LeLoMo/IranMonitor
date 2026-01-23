using Barometer.Services;
using Microsoft.AspNetCore.Mvc;

namespace Barometer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PolymarketController : ControllerBase
{
    private readonly IPolymarketService _polymarketService;
    private readonly ILogger<PolymarketController> _logger;

    public PolymarketController(IPolymarketService polymarketService, ILogger<PolymarketController> logger)
    {
        _polymarketService = polymarketService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var result = await _polymarketService.GetMarketDataAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Polymarket data");
            return StatusCode(503, new { status = "Service Degraded", message = "Polymarket service temporarily unavailable" });
        }
    }
}
