using Microsoft.AspNetCore.Mvc;
using Observability.Api.Meters;
using Observability.Api.Services;
using System.Diagnostics;

namespace Observability.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StoreController : ControllerBase
{
    private readonly BusinessMetrics _businessMetrics;
    private readonly RedisMetricsService _metricsService;
    private readonly Random _random = new();
    private readonly ILogger<StoreController> _logger;

    public StoreController(BusinessMetrics businessMetrics, RedisMetricsService metricsService, ILogger<StoreController> logger)
    {
        _businessMetrics = businessMetrics;
        _metricsService = metricsService;
        _logger = logger;
    }

    [HttpGet("join")]
    public async Task<IActionResult> Join()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Delay(_random.Next(50, 200)); // Simulate processing time
            _businessMetrics.UserJoined();
            _logger.LogInformation("User joined the store.");

            return Ok("User joined the store.");
        }
        finally
        {
            stopwatch.Stop();
            _metricsService.RecordHistogram("api_request_duration_ms", stopwatch.ElapsedMilliseconds,
                new Dictionary<string, string> { { "endpoint", "join" }, { "method", "GET" } });
        }
    }

    [HttpGet("look-around")]
    public async Task<IActionResult> LookAround()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Delay(_random.Next(100, 300)); // Simulate processing time
            _businessMetrics.UserLookingAround();
            _logger.LogInformation("User is looking around.");

            return Ok("User is looking around.");
        }
        finally
        {
            stopwatch.Stop();
            _metricsService.RecordHistogram("api_request_duration_ms", stopwatch.ElapsedMilliseconds,
                new Dictionary<string, string> { { "endpoint", "look-around" }, { "method", "GET" } });
        }
    }

    [HttpGet("leave")]
    public async Task<IActionResult> Leave()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Delay(_random.Next(25, 100)); // Simulate processing time
            _businessMetrics.UserLeft();
            _logger.LogInformation("User left the store.");

            return Ok("User left the store.");
        }
        finally
        {
            stopwatch.Stop();
            _metricsService.RecordHistogram("api_request_duration_ms", stopwatch.ElapsedMilliseconds,
                new Dictionary<string, string> { { "endpoint", "leave" }, { "method", "GET" } });
        }
    }

    [HttpGet("served")]
    public async Task<IActionResult> Served()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Delay(_random.Next(200, 500)); // Simulate processing time
            _businessMetrics.UserServed();
            _logger.LogInformation("User has been served.");

            return Ok("User has been served.");
        }
        finally
        {
            stopwatch.Stop();
            _metricsService.RecordHistogram("api_request_duration_ms", stopwatch.ElapsedMilliseconds,
                new Dictionary<string, string> { { "endpoint", "served" }, { "method", "GET" } });
        }
    }

    [HttpPost("simulate")]
    public async Task<IActionResult> SimulateUserAction()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var actions = new List<string> { "join", "look", "leave", "serve" };
            var action = actions[_random.Next(actions.Count)];

            await Task.Delay(_random.Next(50, 200)); // Simulate processing time

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
        finally
        {
            stopwatch.Stop();
            _metricsService.RecordHistogram("api_request_duration_ms", stopwatch.ElapsedMilliseconds,
                new Dictionary<string, string> { { "endpoint", "simulate" }, { "method", "POST" } });
        }
    }

    [HttpGet("error")]
    public async Task<IActionResult> Error()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Delay(_random.Next(100, 300)); // Simulate processing time
            _metricsService.IncrementCounter("api_errors", 1, new Dictionary<string, string> { { "endpoint", "error" } });
            _logger.LogError("User encountered an error.");

            return StatusCode(500, "User encountered an error.");
        }
        finally
        {
            stopwatch.Stop();
            _metricsService.RecordHistogram("api_request_duration_ms", stopwatch.ElapsedMilliseconds,
                new Dictionary<string, string> { { "endpoint", "error" }, { "method", "GET" } });
        }
    }
}
