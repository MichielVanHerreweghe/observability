otelcol.receiver.otlp "otlp" {
  grpc {
    endpoint = "0.0.0.0:4317"
    max_recv_msg_size = "32MiB"
    max_concurrent_streams = 16
  }
  
  http {
    endpoint = "0.0.0.0:4318"
    max_request_body_size = "32MiB"
  }

  output {
    logs = [otelcol.exporter.loki.loki.input]
  }
}

// Scrape Redis metrics from the Redis exporter sidecar
prometheus.scrape "app_metrics" {
  targets = [
    {
      __address__ = "app:8080",
      job         = "observability-api",
    },
  ]

  forward_to      = [prometheus.remote_write.default.receiver]
  scrape_interval = "5s"
  scrape_timeout  = "3s"
  metrics_path    = "/metrics"
}

otelcol.exporter.loki "loki" {
  forward_to = [loki.write.default.receiver]
}

prometheus.remote_write "default" {
  endpoint {
    url = "http://prometheus:9090/api/v1/write"
    
    // Optimize for faster delivery
    remote_timeout = "10s"
    
    queue_config {
      capacity = 10000
      max_shards = 50
      min_shards = 1
      max_samples_per_send = 2000
      batch_send_deadline = "2s"
      min_backoff = "30ms"
      max_backoff = "100ms"
    }
  }
}

loki.write "default" {
  endpoint {
    url = "http://loki:3100/loki/api/v1/push"
  }
}