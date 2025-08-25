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
    metrics = [otelcol.exporter.prometheus.prometheus.input]
    logs    = [otelcol.exporter.loki.loki.input]
  }
}

otelcol.exporter.prometheus "prometheus" {
  forward_to = [prometheus.remote_write.default.receiver]
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