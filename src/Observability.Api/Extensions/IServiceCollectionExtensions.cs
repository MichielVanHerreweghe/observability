using Observability.Api.Meters;
using OpenTelemetry.Metrics;
using System.Diagnostics.Metrics;

namespace Observability.Api.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddOpenTelemetryServices(configuration);

        return services;
    }

    private static IServiceCollection AddOpenTelemetryServices(this IServiceCollection services, IConfiguration configuration)
    {
        string? otlpEndpoint = configuration.GetConnectionString("OTLP_ENDPOINT");

        if (string.IsNullOrEmpty(otlpEndpoint))
            throw new ArgumentException("OTLP endpoint is not configured in the connection strings.");

        services.AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddMeter("Store.Api");
            metrics.AddOtlpExporter(exporter =>
                exporter.Endpoint = new Uri(otlpEndpoint)
            );
        });

        services.AddSingleton(new Meter("Store.Api", "1.0.0"));
        services.AddSingleton<BusinessMetrics>();

        return services;
    }
}
