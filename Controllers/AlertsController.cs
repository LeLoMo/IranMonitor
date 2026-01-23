using Barometer.Services;
using Microsoft.AspNetCore.Mvc;

namespace Barometer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(IAlertService alertService, ILogger<AlertsController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var result = await _alertService.GetAlertStatusAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching alert data");
            return StatusCode(503, new { status = "Service Degraded", message = "Alert service temporarily unavailable" });
        }
    }
}
