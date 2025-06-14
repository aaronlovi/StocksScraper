# 3. Logging and Metrics Stack

## Status
Accepted

## Context
Centralized logging and metrics are required for observability, troubleshooting, and future monitoring. The stack must integrate with .NET and support both local and production environments.

## Decision
Serilog is used for logging (to Elastic/Kibana and stdout), and Prometheus/Grafana are planned for metrics.

## Consequences
- Centralized log aggregation and future observability.
- Easy integration with .NET and Docker environments.
- Metrics instrumentation is planned for cache and other components.

## Alternatives Considered
- NLog
- log4net
- Other metrics solutions

---
