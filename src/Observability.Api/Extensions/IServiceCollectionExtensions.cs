using Observability.Api.Meters;
using Observability.Api.Services;
using OpenTelemetry.Logs;
using StackExchange.Redis;

namespace Observability.Api.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddRedisServices(configuration);
        services.AddLoggingServices(configuration);

        return services;
    }

    private static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Redis connection
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<IConnectionMultiplexer>>();
            logger.LogInformation("Connecting to Redis at: {ConnectionString}", redisConnectionString);

            try
            {
                // Add connection resilience
                var config = ConfigurationOptions.Parse(redisConnectionString);
                config.AbortOnConnectFail = false; // Keep trying to connect
                config.ConnectTimeout = 10000; // 10 seconds timeout
                config.SyncTimeout = 5000; // 5 seconds sync timeout
                config.ConnectRetry = 3; // Retry 3 times

                var connection = ConnectionMultiplexer.Connect(config);
                logger.LogInformation("Redis connection established: {IsConnected}", connection.IsConnected);
                return connection;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to Redis");
                throw;
            }
        });

        // Add Redis metrics service
        services.AddSingleton<RedisMetricsService>();
        services.AddSingleton<BusinessMetrics>();

        return services;
    }
    private static IServiceCollection AddLoggingServices(this IServiceCollection services, IConfiguration configuration)
    {
        string? otlpEndpoint = configuration.GetConnectionString("OTLP_ENDPOINT");

        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            services.AddOpenTelemetry()
            .WithLogging(logs =>
            {
                logs.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = new Uri(otlpEndpoint);
                    exporter.ExportProcessorType = OpenTelemetry.ExportProcessorType.Batch;
                });
            });
        }

        return services;
    }
}
