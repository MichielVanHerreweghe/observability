using System.Diagnostics.Metrics;

namespace Observability.Api.Meters;

public class BusinessMetrics
{
    private int _waitingUsers;
    private int _activeUsers;
    private int _servedUsers;
    private int _totalUsers;

    public BusinessMetrics(Meter meter)
    {
        meter.CreateObservableGauge(
            "users_waiting",
            observeValue: () => _waitingUsers,
            unit: "users",
            description: "Number of users waiting for service."
        );

        meter.CreateObservableGauge(
            "users_active",
            observeValue: () => _activeUsers,
            unit: "users",
            description: "Number of users looking around."
        );

        meter.CreateObservableGauge(
            "users_served",
            observeValue: () => _servedUsers,
            unit: "users",
            description: "Number of users served."
        );

        meter.CreateObservableGauge(
            "users_total",
            observeValue: () => _totalUsers,
            unit: "users",
            description: "Total numbers of users."
        );
    }

    public void UserJoined()
    {
        _waitingUsers++;
        _totalUsers++;
    }

    public void UserLookingAround()
    {
        if (_waitingUsers > 0)
        {
            _waitingUsers--;
            _activeUsers++;
        }
    }

    public void UserLeft()
    {
        if (_activeUsers > 0)
        {
            _activeUsers--;
        }
    }

    public void UserServed()
    {
        if (_activeUsers > 0)
        {
            _activeUsers--;
            _servedUsers++;
        }
    }
}