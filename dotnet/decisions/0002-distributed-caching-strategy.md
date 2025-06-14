# 2. Distributed Caching Strategy

## Status
Accepted

## Context
The solution requires distributed caching to improve performance, support distributed locking, and provide cache metrics. The cache must be configurable for different environments (production, development).

## Decision
Redis is chosen as the distributed cache, with an in-memory fallback for local and development environments.

## Consequences
- Enables distributed locking and cache metrics.
- Provides high performance and reliability in production.
- Allows for local development without Redis by using in-memory cache.

## Alternatives Considered
- Memcached
- No cache
- Custom caching solution

---
