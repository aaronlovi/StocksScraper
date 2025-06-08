# Stocks.Persistence

## Overview

The `Stocks.Persistence` project provides database and distributed caching infrastructure for the StocksScraper solution. It is responsible for data access, migrations, distributed caching, distributed locking, and encryption utilities.

## Main Components

### 1. Database Management
- **DbmService**: Central service for database operations, including:
  - Company and company name management (CRUD, bulk insert, pagination)
  - Data point and taxonomy management (insert, bulk insert, retrieval)
  - Submission management (insert, bulk insert with duplicate handling, retrieval)
  - ID generation for new records
  - Table management (truncate, drop all)
- **PostgresExecutor**: Executes SQL statements with retry logic, connection pooling, and transaction support.
- **DbMigrations**: Applies database schema migrations using Evolve and embedded SQL scripts.

### 2. Distributed Caching
- **CacheService**: Provides a caching abstraction with metrics for cache hits, misses, and errors.
- **CacheHostConfig**: Configures distributed cache (Redis or in-memory) based on app settings.
- **CacheOptions**: Represents cache configuration options.
- **CacheExecutor**: Executes cache statements.
- **Cache Statement Base Classes**: Abstract base classes for cache read/write/invalidate operations.

### 3. Distributed Locking
- **IDistributedLockService**: Interface for distributed lock acquisition.
- **RedisDistributedLockService**: Implements distributed locking using Redis.
- **InMemoryDistributedLockService**: Implements distributed locking using in-memory cache and semaphores.
- **IDistributedLock**: Interface for lock objects.
- **RedisDistributedLock / InMemoryDistributedLock**: Concrete lock implementations for Redis and in-memory.

### 4. Encryption
- **Encryption**: Static utility for encrypting and decrypting byte arrays using AES and password-based key derivation.

### 5. DTOs and Statements
- **DTOs**: Data transfer objects for taxonomies and concepts.
- **Statements**: Classes for specific database operations (bulk insert, get, truncate, etc.).

---

This project is designed to be used by other services in the solution for reliable, scalable, and secure data persistence and caching.
