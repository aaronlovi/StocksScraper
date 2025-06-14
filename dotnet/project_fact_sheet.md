# Project Fact Sheet

## Purpose
A C#/.NET backend for collecting, processing, and serving stock market data (initially US, via SEC EDGAR bulk downloads) for research and public consumption. The long-term goal is to power a B2C platform for retail investors and individuals to research stocks and markets, with a future consumer-facing frontend. The project also aspires to provide a robust data API (gRPC and potentially other protocols) for frontends and other services.

## Primary user stories
- As the project owner, I want to scrape and analyze stock market data for my own research.
- As a retail investor, I want to quickly generate reports and screen stocks using up-to-date, historical, and cross-company data.
- As a user, I want to run offline jobs to analyze and screen stocks across the market.
- As a user, I want to list all available financial statements or taxonomy concepts for a company, so I can explore what data is available.
- As a user, I want to display a specific financial statement or taxonomy concept hierarchy for a company as of a given date, so I can analyze historical financials.
- As a user, I want to export financial statement data in CSV format for further analysis or integration with other tools.
- As a user, I want to filter and limit the depth of statement hierarchies to avoid excessive or irrelevant data.
- As a frontend or service consumer, I want to retrieve company and financial data via a public API (gRPC or other protocols) for use in applications or further processing.
- As the project owner, I want robust error handling and logging so that failures do not crash the ETL process and issues can be diagnosed.
- As the project owner, I want to extend the system to support new taxonomies, data sources, or output formats as requirements evolve.

## Architecture snapshot
- Data ingestion: .NET console app scrapes SEC EDGAR bulk data (and, in future, other sources/markets) and loads it into a Docker-hosted database.
- Data persistence: Centralized PostgreSQL database (in Docker), managed by a custom data access layer with retry logic, transactional support, and distributed-safe ID generation.
- Data service: .NET Worker Service and supporting libraries for ETL, data access, and business logic.
- Data API service: gRPC server (and potentially WebSockets/other protocols) to serve data to frontends and other consumers; extensible for future protocols and endpoints.
- Shared utilities and models: Common helpers, core financial/company/taxonomy/ETL models, enums, constants, and utilities for logging, metrics, concurrency, serialization, and more, used across all projects.
- Distributed caching: Redis or in-memory cache, with distributed locking (for cache consistency), configurable per environment.
- Logging: Serilog to Elastic/Kibana (stdout logging for future Loki/Grafana compatibility).
- Metrics: Planned Prometheus/Grafana integration; cache metrics (hits, misses, errors) instrumented.
- Database migrations: Automatic migrations on startup to ensure schema consistency.
- Future: API and frontend for public/retail investor access.

## Tech stack
- .NET 8 (C#)
- Serilog (logging)
- Elastic/Kibana (log aggregation)
- Docker (database and service hosting)
- PostgreSQL (database)
- Redis (distributed cache; in-memory fallback for local/dev)
- StackExchange.Redis (Redis client)
- Npgsql (PostgreSQL client)
- gRPC (data API)
- Shared utilities/models (custom library, including core financial, company, taxonomy, and ETL data structures)
- Planned: Prometheus/Grafana (metrics), JWT-based auth, cloud/on-prem hosting, WebSockets/other protocols for data API
- Core: xUnit (unit testing), SpecFlow or similar (Gherkin-style BDD scenarios) for automated and behavior-driven testing of most new features and changes

## Cross-cutting rules
- Logging: Serilog to Elastic/Kibana; logs to stdout for future Loki/Grafana scraping.
- Metrics: Prometheus/Grafana integration planned; cache metrics (hits, misses, errors) tracked.
- Distributed caching: Redis or in-memory, with distributed locking for cache consistency.
- Database: Automatic migrations, transactional support, and robust error handling in persistence.
- Shared code: Utilities, models, and helpers (including core data models) are reused across all projects for consistency and maintainability.
- Security: No user logins yet; plan for Google/Facebook JWT or custom login in future.
- Privacy/Compliance: No requirements yet; to be determined as user data and features expand.
- Error handling: Robust error handling and logging throughout ETL, data processing, caching, and persistence.
- Automated and behavior-driven testing (xUnit and Gherkin/SpecFlow) is a standard requirement for most new features and changes. Contributors must provide tests or scenarios that prove new functionality works as intended, unless explicitly stated otherwise.
- All features and projects must be broken down into a Kanban-style list of tasks, each estimated at 2 hours or less, before implementation begins.

## Out-of-scope
- Real-time or intraday stock quotes/data (only daily/delayed data is planned)
- GraphQL APIs (not planned/preferred)
- Trading or brokerage features
- User authentication/authorization (for now)
- Any compliance guarantees (for now)
