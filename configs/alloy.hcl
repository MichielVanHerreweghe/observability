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
  }
}

loki.write "default" {
  endpoint {
    url = "http://loki:3100/loki/api/v1/push"
  }
}