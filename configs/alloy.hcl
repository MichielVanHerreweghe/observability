otelcol.receiver.otlp "otlp" {
  grpc {
    endpoint = "0.0.0.0:4317"
  }
  
  http {
    endpoint = "0.0.0.0:4318"
  }

  output {
    metrics = [otelcol.exporter.prometheus.prometheus.input]
  }
}

otelcol.exporter.prometheus "prometheus" {
  forward_to = [prometheus.remote_write.default.receiver]
}

prometheus.remote_write "default" {
  endpoint {
    url = "http://prometheus:9090/api/v1/write"
  }
}