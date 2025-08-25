using Microsoft.AspNetCore.Mvc;
using Observability.Api.Meters;
using OpenTelemetry.Metrics;

namespace Observability.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StoreController : ControllerBase
{
    private readonly BusinessMetrics _businessMetrics;
    private readonly Random _random = new();
    private readonly ILogger<StoreController> _logger;

    public StoreController(BusinessMetrics businessMetrics, ILogger<StoreController> logger)
    {
        _businessMetrics = businessMetrics;
        _logger = logger;
    }

    [HttpGet("join")]
    public IActionResult Join()
    {
        _businessMetrics.UserJoined();
        _logger.LogInformation("User joined the store.");
        return Ok("User joined the store.");
    }

    [HttpGet("look-around")]
    public IActionResult LookAround()
    {
        _businessMetrics.UserLookingAround();
        _logger.LogInformation("User is looking around.");
        return Ok("User is looking around.");
    }

    [HttpGet("leave")]
    public IActionResult Leave()
    {
        _businessMetrics.UserLeft();
        _logger.LogInformation("User left the store.");
        return Ok("User left the store.");
    }

    [HttpGet("served")]
    public IActionResult Served()
    {
        _businessMetrics.UserServed();
        _logger.LogInformation("User has been served.");
        return Ok("User has been served.");
    }

    [HttpPost("simulate")]
    public IActionResult SimulateUserAction()
    {
        var actions = new List<string> { "join", "look", "leave", "serve" };
        var action = actions[_random.Next(actions.Count)];

        switch (action)
        {
            case "join":
                _businessMetrics.UserJoined();
                break;
            case "look":
                _businessMetrics.UserLookingAround();
                break;
            case "leave":
                _businessMetrics.UserLeft();
                break;
            case "serve":
                _businessMetrics.UserServed();
                break;
        }

        return Ok(new { Action = action, Message = $"Simulated {action}" });
    }

    [HttpGet("error")]
    public IActionResult Error()
    {
        _logger.LogError("User encountered an error.");
        return Ok("User encountered an error.");
    }
}
