using Newtonsoft.Json;
using StackExchange.Redis;
using System.Diagnostics.Metrics;

namespace Observability.Api.Services;

public class RedisMetricsService : IDisposable
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _redis;
    private readonly Timer _flushTimer;
    private readonly Dictionary<string, double> _counters = new();
    private readonly Dictionary<string, double> _gauges = new();
    private readonly Dictionary<string, List<double>> _histograms = new();
    private readonly object _lock = new();
    private readonly ILogger<RedisMetricsService> _logger;
    private bool _disposed;

    public RedisMetricsService(IConnectionMultiplexer redis, ILogger<RedisMetricsService> logger)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _logger = logger;

        _logger.LogInformation("RedisMetricsService initialized with Redis connection: {IsConnected}", _redis.IsConnected);

        // Flush metrics to Redis every 5 seconds
        _flushTimer = new Timer(FlushMetrics, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }
    public void IncrementCounter(string name, double value = 1, Dictionary<string, string>? tags = null)
    {
        var key = CreateMetricKey(name, "counter", tags);
        lock (_lock)
        {
            _counters[key] = _counters.GetValueOrDefault(key, 0) + value;
        }
        _logger.LogDebug("Incremented counter {Key} by {Value}. Current value: {CurrentValue}",
            key, value, _counters[key]);
    }

    public void SetGauge(string name, double value, Dictionary<string, string>? tags = null)
    {
        var key = CreateMetricKey(name, "gauge", tags);
        lock (_lock)
        {
            _gauges[key] = value;
        }
    }

    public void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null)
    {
        var key = CreateMetricKey(name, "histogram", tags);
        lock (_lock)
        {
            if (!_histograms.ContainsKey(key))
                _histograms[key] = new List<double>();

            _histograms[key].Add(value);

            // Keep only last 1000 values to prevent memory issues
            if (_histograms[key].Count > 1000)
                _histograms[key].RemoveAt(0);
        }
    }

    private string CreateMetricKey(string name, string type, Dictionary<string, string>? tags)
    {
        var key = $"metrics:{type}:{name}";

        if (tags != null && tags.Any())
        {
            var tagString = string.Join(",", tags.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            key += $":{tagString}";
        }

        return key;
    }

    private void FlushMetrics(object? state)
    {
        if (_disposed) return;

        try
        {
            var batch = _database.CreateBatch();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Dictionary<string, double> countersToFlush;
            Dictionary<string, double> gaugesToFlush;
            Dictionary<string, List<double>> histogramsToFlush;

            // Copy data under lock
            lock (_lock)
            {
                countersToFlush = new Dictionary<string, double>(_counters);
                gaugesToFlush = new Dictionary<string, double>(_gauges);
                histogramsToFlush = new Dictionary<string, List<double>>();

                foreach (var kvp in _histograms)
                {
                    histogramsToFlush[kvp.Key] = new List<double>(kvp.Value);
                    kvp.Value.Clear(); // Clear after copying
                }

                // Reset counters after copying (gauges keep their values)
                _counters.Clear();
            }

            _logger.LogInformation("Flushing metrics to Redis: {CounterCount} counters, {GaugeCount} gauges, {HistogramCount} histograms",
                countersToFlush.Count, gaugesToFlush.Count, histogramsToFlush.Count);

            // Flush counters
            foreach (var counter in countersToFlush)
            {
                var metricData = new
                {
                    value = counter.Value,
                    timestamp = timestamp,
                    type = "counter"
                };

                var json = JsonConvert.SerializeObject(metricData);
                _ = batch.StringSetAsync(counter.Key, json);
                _ = batch.KeyExpireAsync(counter.Key, TimeSpan.FromMinutes(30)); // Expire after 30 minutes
                _logger.LogDebug("Queued counter {Key} = {Value}", counter.Key, counter.Value);
            }

            // Flush gauges
            foreach (var gauge in gaugesToFlush)
            {
                var metricData = new
                {
                    value = gauge.Value,
                    timestamp = timestamp,
                    type = "gauge"
                };

                var json = JsonConvert.SerializeObject(metricData);
                _ = batch.StringSetAsync(gauge.Key, json);
                _ = batch.KeyExpireAsync(gauge.Key, TimeSpan.FromMinutes(30));
                _logger.LogDebug("Queued gauge {Key} = {Value}", gauge.Key, gauge.Value);
            }

            // Flush histograms
            foreach (var histogram in histogramsToFlush)
            {
                if (histogram.Value.Any())
                {
                    var values = histogram.Value;
                    var metricData = new
                    {
                        count = values.Count,
                        sum = values.Sum(),
                        min = values.Min(),
                        max = values.Max(),
                        avg = values.Average(),
                        p50 = GetPercentile(values, 0.5),
                        p95 = GetPercentile(values, 0.95),
                        p99 = GetPercentile(values, 0.99),
                        timestamp = timestamp,
                        type = "histogram"
                    };

                    var json = JsonConvert.SerializeObject(metricData);
                    _ = batch.StringSetAsync(histogram.Key, json);
                    _ = batch.KeyExpireAsync(histogram.Key, TimeSpan.FromMinutes(30));
                    _logger.LogDebug("Queued histogram {Key} with {Count} values", histogram.Key, values.Count);
                }
            }

            batch.Execute();
            _logger.LogInformation("Successfully executed Redis batch write");
        }
        catch (Exception ex)
        {
            // Log the error - in a real application, use proper logging
            _logger.LogError(ex, "Error flushing metrics to Redis");
        }
    }

    private double GetPercentile(List<double> values, double percentile)
    {
        if (!values.Any()) return 0;

        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));

        return sorted[index];
    }

    public async Task<Dictionary<string, object>> GetMetricsAsync(string pattern = "metrics:*")
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: pattern);

        var metrics = new Dictionary<string, object>();

        foreach (var key in keys)
        {
            var value = await _database.StringGetAsync(key);
            if (value.HasValue)
            {
                try
                {
                    var data = JsonConvert.DeserializeObject(value!);
                    metrics[key!] = data!;
                }
                catch
                {
                    metrics[key!] = value.ToString();
                }
            }
        }

        return metrics;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _flushTimer?.Dispose();
            FlushMetrics(null); // Final flush
        }
    }
}
