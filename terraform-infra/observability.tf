############################
# GRAFANA (Visualization)
############################

resource "helm_release" "grafana" {
  name       = "grafana"
  namespace  = kubernetes_namespace.monitoring.metadata[0].name
  repository = "https://grafana.github.io/helm-charts"
  chart      = "grafana"
  version    = "10.2.0"

  depends_on = [kubernetes_namespace.monitoring, helm_release.loki, helm_release.tempo, helm_release.prometheus, helm_release.ingress_nginx]

  set {
    name  = "adminPassword"
    value = var.grafana_admin_password
  }

  set {
    name  = "ingress.enabled"
    value = "true"
  }

  set {
    name  = "ingress.ingressClassName"
    value = "nginx"
  }

  set {
    name  = "ingress.hosts[0]"
    value = "overflow-grafana.helios"
  }

  set {
    name  = "persistence.enabled"
    value = "true"
  }

  set {
    name  = "persistence.size"
    value = "10Gi"
  }

  # Configure datasources with proper correlations
  values = [<<EOF
datasources:
  datasources.yaml:
    apiVersion: 1
    datasources:
      - name: Prometheus
        type: prometheus
        access: proxy
        url: http://prometheus-server.monitoring.svc.cluster.local
        isDefault: false
        editable: true
        jsonData:
          timeInterval: 30s
          exemplarTraceIdDestinations:
            - name: trace_id
              datasourceUid: tempo

      - name: Tempo
        type: tempo
        access: proxy
        url: http://tempo.monitoring.svc.cluster.local:3100
        isDefault: false
        editable: true
        jsonData:
          tracesToLogs:
            datasourceUid: loki
            tags:
              - service_name
              - service.name
            mappedTags:
              - key: service.name
                value: service_name
            mapTagNamesEnabled: true
            spanStartTimeShift: -1h
            spanEndTimeShift: 1h
            filterByTraceID: true
            filterBySpanID: false
          tracesToMetrics:
            datasourceUid: prometheus
            tags:
              - key: service.name
                value: service_name
            queries:
              - name: Request Rate
                query: rate(http_server_request_duration_seconds_count{$$__tags}[5m])
              - name: Request Duration
                query: histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket{$$__tags}[5m]))
          serviceMap:
            datasourceUid: prometheus
          nodeGraph:
            enabled: true
          search:
            hide: false
          lokiSearch:
            datasourceUid: loki

      - name: Loki
        type: loki
        access: proxy
        url: http://loki.monitoring.svc.cluster.local:3100
        isDefault: true
        editable: true
        jsonData:
          derivedFields:
            - datasourceUid: tempo
              matcherRegex: "trace_id=(\\w+)"
              name: TraceID
              url: '$${__value.raw}'
            - datasourceUid: tempo
              matcherRegex: "traceID=(\\w+)"
              name: TraceID
              url: '$${__value.raw}'
            - datasourceUid: tempo
              matcherRegex: '"trace_id":"(\\w+)"'
              name: TraceID
              url: '$${__value.raw}'
EOF
  ]

}

############################
# LOKI (Log Aggregation)
############################

resource "helm_release" "loki" {
  name       = "loki"
  namespace  = kubernetes_namespace.monitoring.metadata[0].name
  repository = "https://grafana.github.io/helm-charts"
  chart      = "loki-stack"
  version    = "2.10.0"


  set {
    name  = "loki.enabled"
    value = "true"
  }

  set {
    name  = "promtail.enabled"
    value = "true"
  }

  set {
    name  = "grafana.enabled"
    value = "false" # We already have Grafana
  }

  set {
    name  = "loki.persistence.enabled"
    value = "true"
  }

  set {
    name  = "loki.persistence.size"
    value = "10Gi"
  }
}

############################
# TEMPO (Distributed Tracing)
############################

resource "helm_release" "tempo" {
  name       = "tempo"
  namespace  = kubernetes_namespace.monitoring.metadata[0].name
  repository = "https://grafana.github.io/helm-charts"
  chart      = "tempo"
  version    = "1.7.0"


  set {
    name  = "tempo.receivers.otlp.protocols.http.endpoint"
    value = "0.0.0.0:4318"
  }

  set {
    name  = "tempo.receivers.otlp.protocols.grpc.endpoint"
    value = "0.0.0.0:4317"
  }

  set {
    name  = "persistence.enabled"
    value = "true"
  }

  set {
    name  = "persistence.size"
    value = "10Gi"
  }
}

############################
# OpenTelemetry Collector (OTLP Gateway)
############################

resource "helm_release" "otel_collector" {
  name       = "otel-collector"
  namespace  = kubernetes_namespace.monitoring.metadata[0].name
  repository = "https://open-telemetry.github.io/opentelemetry-helm-charts"
  chart      = "opentelemetry-collector"
  version    = "0.142.0"

  depends_on = [kubernetes_namespace.monitoring, helm_release.tempo, helm_release.loki, helm_release.prometheus]

  values = [<<EOF
mode: deployment

replicaCount: 2

image:
  repository: otel/opentelemetry-collector-contrib

config:
  receivers:
    otlp:
      protocols:
        grpc:
          endpoint: 0.0.0.0:4317
        http:
          endpoint: 0.0.0.0:4318
  
  processors:
    batch:
      timeout: 10s
      send_batch_size: 1024
    
    memory_limiter:
      check_interval: 1s
      limit_percentage: 75
      spike_limit_percentage: 20
    
    resource:
      attributes:
        - key: cluster.name
          value: overflow-k8s
          action: upsert
  
  exporters:
    otlp/tempo:
      endpoint: tempo.monitoring.svc.cluster.local:4317
      tls:
        insecure: true
    
    prometheusremotewrite:
      endpoint: http://prometheus-server.monitoring.svc.cluster.local/api/v1/write
      resource_to_telemetry_conversion:
        enabled: true
      add_metric_suffixes: true
    
    debug:
      verbosity: detailed
  
  service:
    pipelines:
      traces:
        receivers: [otlp]
        processors: [memory_limiter, batch, resource]
        exporters: [otlp/tempo]
      
      metrics:
        receivers: [otlp]
        processors: [memory_limiter, batch, resource]
        exporters: [prometheusremotewrite]
      
      logs:
        receivers: [otlp]
        processors: [memory_limiter, batch, resource]
        exporters: [debug]

ports:
  otlp:
    enabled: true
    containerPort: 4317
    servicePort: 4317
    hostPort: 4317
    protocol: TCP
  otlp-http:
    enabled: true
    containerPort: 4318
    servicePort: 4318
    hostPort: 4318
    protocol: TCP

resources:
  limits:
    cpu: 1000m
    memory: 2Gi
  requests:
    cpu: 200m
    memory: 400Mi

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 10
  targetCPUUtilizationPercentage: 80

service:
  type: ClusterIP
EOF
  ]
}

############################
# PROMETHEUS (Metrics)
############################

resource "helm_release" "prometheus" {
  name       = "prometheus"
  namespace  = kubernetes_namespace.monitoring.metadata[0].name
  repository = "https://prometheus-community.github.io/helm-charts"
  chart      = "prometheus"
  version    = "25.8.0"

  depends_on = [kubernetes_namespace.monitoring, helm_release.ingress_nginx]


  set {
    name  = "server.ingress.enabled"
    value = "true"
  }

  set {
    name  = "server.ingress.ingressClassName"
    value = "nginx"
  }

  set {
    name  = "server.ingress.hosts[0]"
    value = "overflow-prometheus.helios"
  }

  set {
    name  = "server.persistentVolume.enabled"
    value = "true"
  }

  set {
    name  = "server.persistentVolume.size"
    value = "10Gi"
  }

  set {
    name  = "alertmanager.enabled"
    value = "false" # Disable for now, can enable later
  }

  # Enable remote write receiver for OTLP metrics
  set {
    name  = "server.extraFlags[0]"
    value = "web.enable-remote-write-receiver"
  }

  # Keep scraping for infrastructure metrics
  set {
    name  = "prometheus-pushgateway.enabled"
    value = "false"
  }

  set {
    name  = "prometheus-node-exporter.enabled"
    value = "true"
  }

  set {
    name  = "kube-state-metrics.enabled"
    value = "true"
  }
}

