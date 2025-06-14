# 1. Database Technology Choice

## Status
Accepted

## Context
The project requires a reliable, transactional, and scalable database for storing stock market, company, and financial data. The database must support bulk operations, schema migrations, and integration with .NET applications.

## Decision
PostgreSQL (hosted in Docker) is chosen as the primary database technology for persistence.

## Consequences
- Enables transactional support, bulk operations, and robust schema migrations.
- Integrates well with .NET via Npgsql.
- Supports future scaling and advanced SQL features.
- Requires Docker for local development and deployment.

## Alternatives Considered
- SQL Server
- MySQL
- SQLite
- NoSQL options (rejected for this use case)

---
