using Microsoft.AspNetCore.Mvc;
using Observability.Api.Services;

namespace Observability.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly RedisMetricsService _metricsService;
    private readonly ILogger<TestController> _logger;

    public TestController(RedisMetricsService metricsService, ILogger<TestController> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        try
        {
            var metrics = await _metricsService.GetMetricsAsync();
            return Ok(new { Count = metrics.Count, Metrics = metrics });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metrics from Redis");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("generate")]
    public IActionResult GenerateTestMetrics()
    {
        _logger.LogInformation("Generating test metrics...");

        try
        {
            // Generate some test metrics
            _metricsService.IncrementCounter("test_requests_total", 1, new Dictionary<string, string>
            {
                ["method"] = "GET",
                ["status"] = "200"
            });

            _metricsService.SetGauge("test_active_connections", 42, new Dictionary<string, string>
            {
                ["service"] = "api"
            });

            _metricsService.RecordHistogram("test_request_duration_seconds", 0.125, new Dictionary<string, string>
            {
                ["method"] = "GET"
            });

            _logger.LogInformation("Test metrics generated successfully");
            return Ok(new { message = "Test metrics generated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate test metrics");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
