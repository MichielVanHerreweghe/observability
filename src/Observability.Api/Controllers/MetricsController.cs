using Microsoft.AspNetCore.Mvc;
using Observability.Api.Services;
using System.Text;

namespace Observability.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class MetricsController : ControllerBase
{
    private readonly RedisMetricsService _metricsService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(RedisMetricsService metricsService, ILogger<MetricsController> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    [HttpGet]
    [Produces("text/plain")]
    public async Task<IActionResult> GetMetrics()
    {
        try
        {
            _logger.LogInformation("Retrieving metrics for Prometheus...");
            var metrics = await _metricsService.GetMetricsAsync();
            _logger.LogInformation("Retrieved {Count} metrics from Redis", metrics.Count);

            var prometheusFormat = ConvertToPrometheusFormat(metrics);
            _logger.LogInformation("Generated Prometheus format: {Length} characters", prometheusFormat.Length);

            return Content(prometheusFormat, "text/plain; version=0.0.4");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metrics");
            return StatusCode(500, "Error retrieving metrics");
        }
    }

    private string ConvertToPrometheusFormat(Dictionary<string, object> metrics)
    {
        var sb = new StringBuilder();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var kvp in metrics)
        {
            var key = kvp.Key;
            var jsonString = kvp.Value.ToString();

            if (string.IsNullOrEmpty(jsonString))
                continue;

            try
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonString);
                var data = jsonDoc.RootElement;

                var parsed = ParseMetricKey(key);
                if (parsed == null) continue;

                switch (parsed.Type.ToLower())
                {
                    case "counter":
                        AppendCounterMetric(sb, parsed, data, now);
                        break;
                    case "gauge":
                        AppendGaugeMetric(sb, parsed, data, now);
                        break;
                    case "histogram":
                        AppendHistogramMetric(sb, parsed, data, now);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse metric data for key: {Key}", key);
            }
        }

        return sb.ToString();
    }

    private void AppendCounterMetric(StringBuilder sb, ParsedMetric parsed, System.Text.Json.JsonElement data, long now)
    {
        // Handle both array format and single value format
        double latestValue = 0.0;

        if (data.TryGetProperty("value", out var valueProperty))
        {
            if (valueProperty.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in valueProperty.EnumerateArray())
                {
                    if (item.ValueKind == System.Text.Json.JsonValueKind.Number)
                        latestValue += item.GetDouble();
                }
            }
            else if (valueProperty.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                latestValue = valueProperty.GetDouble();
            }
        }

        sb.AppendLine($"# HELP {parsed.Name} {parsed.Name} total");
        sb.AppendLine($"# TYPE {parsed.Name} counter");
        sb.AppendLine($"{parsed.Name}{FormatLabels(parsed.Labels)} {latestValue} {now}");
    }

    private void AppendGaugeMetric(StringBuilder sb, ParsedMetric parsed, System.Text.Json.JsonElement data, long now)
    {
        // Handle both array format and single value format
        double latestValue = 0.0;

        if (data.TryGetProperty("value", out var valueProperty))
        {
            if (valueProperty.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in valueProperty.EnumerateArray())
                {
                    if (item.ValueKind == System.Text.Json.JsonValueKind.Number)
                        latestValue = item.GetDouble(); // For gauges, use the latest value
                }
            }
            else if (valueProperty.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                latestValue = valueProperty.GetDouble();
            }
        }

        sb.AppendLine($"# HELP {parsed.Name} {parsed.Name} gauge");
        sb.AppendLine($"# TYPE {parsed.Name} gauge");
        sb.AppendLine($"{parsed.Name}{FormatLabels(parsed.Labels)} {latestValue} {now}");
    }

    private void AppendHistogramMetric(StringBuilder sb, ParsedMetric parsed, System.Text.Json.JsonElement data, long now)
    {
        // For simplicity, just expose count and sum for histograms
        double totalCount = 0.0;
        double totalSum = 0.0;

        if (data.TryGetProperty("count", out var countProperty))
        {
            if (countProperty.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in countProperty.EnumerateArray())
                {
                    if (item.ValueKind == System.Text.Json.JsonValueKind.Number)
                        totalCount += item.GetDouble();
                }
            }
            else if (countProperty.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                totalCount = countProperty.GetDouble();
            }
        }

        if (data.TryGetProperty("sum", out var sumProperty))
        {
            if (sumProperty.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in sumProperty.EnumerateArray())
                {
                    if (item.ValueKind == System.Text.Json.JsonValueKind.Number)
                        totalSum += item.GetDouble();
                }
            }
            else if (sumProperty.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                totalSum = sumProperty.GetDouble();
            }
        }

        sb.AppendLine($"# HELP {parsed.Name} {parsed.Name} histogram");
        sb.AppendLine($"# TYPE {parsed.Name} histogram");
        sb.AppendLine($"{parsed.Name}_count{FormatLabels(parsed.Labels)} {totalCount} {now}");
        sb.AppendLine($"{parsed.Name}_sum{FormatLabels(parsed.Labels)} {totalSum} {now}");
    }

    private string FormatLabels(Dictionary<string, string> labels)
    {
        if (labels == null || labels.Count == 0)
            return "";

        var labelPairs = labels.Select(kvp => $"{kvp.Key}=\"{kvp.Value}\"");
        return "{" + string.Join(",", labelPairs) + "}";
    }

    private ParsedMetric? ParseMetricKey(string key)
    {
        // Key format: "metrics:type:name:label1=value1,label2=value2"
        var parts = key.Split(':', 4);
        if (parts.Length < 3 || parts[0] != "metrics")
            return null;

        var labels = new Dictionary<string, string>();
        if (parts.Length >= 4 && !string.IsNullOrEmpty(parts[3]))
        {
            var labelPairs = parts[3].Split(',');
            foreach (var pair in labelPairs)
            {
                var keyValue = pair.Split('=', 2);
                if (keyValue.Length == 2)
                {
                    labels[keyValue[0]] = keyValue[1];
                }
            }
        }

        return new ParsedMetric
        {
            Type = parts[1],
            Name = parts[2],
            Labels = labels
        };
    }

    private class ParsedMetric
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Labels { get; set; } = new();
    }
}
