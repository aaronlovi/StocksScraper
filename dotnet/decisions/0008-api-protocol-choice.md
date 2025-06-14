# 8. API Protocol Choice

## Status
Accepted

## Context
The solution requires a performant, contract-first API for serving data to frontends and other consumers. The API must be cross-platform and extensible for future protocols.

## Decision
Use gRPC for the data API, with future extensibility for WebSockets and other protocols.

## Consequences
- High performance and contract-first development.
- Cross-platform support for clients.
- Future extensibility for additional protocols.

## Alternatives Considered
- REST
- GraphQL
- SOAP

---
