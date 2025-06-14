# 9. Migration Tooling

## Status
Accepted

## Context
Database schema migrations must be reliable, repeatable, and easy to manage across environments. The tool must integrate with .NET and support embedded SQL scripts.

## Decision
Use Evolve for database migrations, with embedded SQL scripts for schema changes.

## Consequences
- Ensures schema consistency and repeatable deployments.
- Integrates with .NET and Docker workflows.
- Supports versioning and rollback of migrations.

## Alternatives Considered
- Flyway
- Liquibase
- Custom migration scripts

---
