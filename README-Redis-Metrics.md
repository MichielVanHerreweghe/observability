# Redis-Based Metrics with Sidecar Exporter

This project uses Redis as a fast cache for metrics with a dedicated Redis exporter sidecar that reads metrics using a custom Lua script. This eliminates the need for HTTP endpoints in the application.

## Benefits

- ⚡ **Instant visibility**: Metrics are available in Redis immediately (no 1-minute delay)
- 🚀 **High performance**: In-memory caching with 5-second batch writes
- 🧹 **Clean separation**: No metrics endpoints cluttering your application API
- 📊 **Native Prometheus**: Direct Prometheus scraping via dedicated exporter
- 🔧 **Custom Lua script**: Intelligent parsing of application metrics from Redis

## Architecture

1. **Application** → Records metrics to `RedisMetricsService`
2. **RedisMetricsService** → Batches and writes to Redis every 5 seconds  
3. **Redis Exporter Sidecar** → Reads from Redis using Lua script, exposes Prometheus format
4. **Prometheus** → Scrapes metrics from Redis exporter every 5 seconds
5. **Grafana** → Visualizes metrics from Prometheus

## Components

### Redis Exporter Sidecar
- **Image**: `oliver006/redis_exporter:latest`
- **Port**: `9121`
- **Script**: Custom Lua script at `/configs/redis-exporter.lua`
- **Function**: Converts Redis JSON metrics to Prometheus format

### Custom Lua Script Features
- ✅ Parses metric keys: `metrics:counter:user_events:event=joined`
- ✅ Converts JSON data to Prometheus format
- ✅ Handles counters, gauges, and histograms
- ✅ Preserves labels and tags
- ✅ Calculates histogram buckets and percentiles

## Available Metrics

### Counters
- `user_events{event="joined|looking_around|left|served"}` - User action events
- `api_errors{endpoint="..."}` - API error counts

### Gauges  
- `users_waiting{service="store"}` - Current waiting users
- `users_active{service="store"}` - Current active users
- `users_served{service="store"}` - Current served users
- `users_total{service="store"}` - Total users

### Histograms
- `api_request_duration_ms{endpoint="...",method="..."}` - API response times with percentiles (p50, p95, p99)

## Quick Start

1. **Start the stack**:
   ```bash
   docker-compose up -d
   ```

2. **Generate some metrics**:
   ```bash
   curl http://localhost:5050/api/store/join
   curl http://localhost:5050/api/store/look-around
   curl http://localhost:5050/api/store/served
   curl http://localhost:5050/api/store/simulate -X POST
   ```

3. **View metrics**:
   - Redis Exporter: http://localhost:9121/metrics
   - Prometheus UI: http://localhost:9090
   - Grafana: http://localhost:3000 (admin/admin)

4. **Direct Redis access**:
   ```bash
   docker exec -it $(docker ps -qf "name=redis") redis-cli
   KEYS "metrics:*"
   GET "metrics:counter:user_events:event=joined"
   ```

5. **Debug the Lua script**:
   ```bash
   # Test the Lua script directly in Redis CLI
   docker exec -it $(docker ps -qf "name=redis") redis-cli
   EVAL "$(cat configs/redis-exporter.lua)" 0
   ```

## Configuration

- **Redis connection**: Automatically configured between services
- **Metric flush interval**: 5 seconds (configurable in `RedisMetricsService`)
- **Prometheus scrape interval**: 5 seconds (configurable in `prometheus.yaml`)
- **Redis TTL**: Metrics expire after 30 minutes to prevent memory issues
- **Lua script**: Located at `configs/redis-exporter.lua`

## Load Testing

Use the provided load test to generate metrics:

```bash
npm install
node simple-load-test.js
```

## Advantages over HTTP Controller Approach

- 🎯 **No application bloat**: Your API stays focused on business logic
- ⚡ **Better performance**: Direct Redis access without HTTP overhead
- 🔒 **Security**: No metrics endpoints exposed in your application
- 🧪 **Testability**: Lua script can be tested independently
- 📈 **Scalability**: Dedicated exporter can handle high metric volumes

You should now see metrics appearing in Prometheus within 5-10 seconds instead of 1+ minutes, with a much cleaner architecture!
