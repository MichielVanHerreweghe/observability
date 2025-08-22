#!/bin/bash

# Redis Exporter configuration with custom Lua script
export REDIS_ADDR="redis:6379"
export REDIS_PASSWORD=""
export WEB_LISTEN_ADDRESS=":9121"
export WEB_TELEMETRY_PATH="/metrics"

# Custom script configuration
export REDIS_EXPORTER_SCRIPT="/app/redis-exporter.lua"

# Start redis_exporter with custom script
exec /usr/local/bin/redis_exporter \
  -redis.addr="$REDIS_ADDR" \
  -web.listen-address="$WEB_LISTEN_ADDRESS" \
  -web.telemetry-path="$WEB_TELEMETRY_PATH" \
  -script="$REDIS_EXPORTER_SCRIPT" \
  -log-format=txt \
  -debug=false
