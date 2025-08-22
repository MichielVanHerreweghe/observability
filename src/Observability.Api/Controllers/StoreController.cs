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

    public StoreController(BusinessMetrics businessMetrics)
    {
        _businessMetrics = businessMetrics;
    }

    [HttpGet("join")]
    public IActionResult Join()
    {
        _businessMetrics.UserJoined();
        return Ok("User joined the store.");
    }

    [HttpGet("look-around")]
    public IActionResult LookAround()
    {
        _businessMetrics.UserLookingAround();
        return Ok("User is looking around.");
    }

    [HttpGet("leave")]
    public IActionResult Leave()
    {
        _businessMetrics.UserLeft();
        return Ok("User left the store.");
    }

    [HttpGet("served")]
    public IActionResult Served()
    {
        _businessMetrics.UserServed();
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
}
