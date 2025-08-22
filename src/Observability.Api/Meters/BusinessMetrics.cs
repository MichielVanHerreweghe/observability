using Observability.Api.Services;

namespace Observability.Api.Meters;

public class BusinessMetrics
{
    private int _waitingUsers;
    private int _activeUsers;
    private int _servedUsers;
    private int _totalUsers;
    private readonly RedisMetricsService _metricsService;
    private readonly Timer _updateTimer;

    public BusinessMetrics(RedisMetricsService metricsService)
    {
        _metricsService = metricsService;

        // Update Redis metrics every 5 seconds
        _updateTimer = new Timer(UpdateRedisMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private void UpdateRedisMetrics(object? state)
    {
        _metricsService.SetGauge("users_waiting", _waitingUsers, new Dictionary<string, string> { { "service", "store" } });
        _metricsService.SetGauge("users_active", _activeUsers, new Dictionary<string, string> { { "service", "store" } });
        _metricsService.SetGauge("users_served", _servedUsers, new Dictionary<string, string> { { "service", "store" } });
        _metricsService.SetGauge("users_total", _totalUsers, new Dictionary<string, string> { { "service", "store" } });
    }

    public void UserJoined()
    {
        _waitingUsers++;
        _totalUsers++;
        _metricsService.IncrementCounter("user_events", 1, new Dictionary<string, string> { { "event", "joined" } });
    }

    public void UserLookingAround()
    {
        if (_waitingUsers > 0)
        {
            _waitingUsers--;
            _activeUsers++;
            _metricsService.IncrementCounter("user_events", 1, new Dictionary<string, string> { { "event", "looking_around" } });
        }
    }

    public void UserLeft()
    {
        if (_activeUsers > 0)
        {
            _activeUsers--;
            _metricsService.IncrementCounter("user_events", 1, new Dictionary<string, string> { { "event", "left" } });
        }
    }

    public void UserServed()
    {
        if (_activeUsers > 0)
        {
            _activeUsers--;
            _servedUsers++;
            _metricsService.IncrementCounter("user_events", 1, new Dictionary<string, string> { { "event", "served" } });
        }
    }
}