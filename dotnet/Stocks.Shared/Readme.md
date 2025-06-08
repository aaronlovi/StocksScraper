# Stocks.Shared

## Overview

The `Stocks.Shared` project provides common utilities, helpers, and shared models for use across the StocksScraper solution. It is designed to promote code reuse and consistency between services.

## Main Components

### 1. Utilities and Helpers
- **ZipFileReader**: Utility for reading and extracting files from ZIP archives.
- **SemaphoreGuard / SemaphoreSlimExtensions**: Helpers for working with semaphores and concurrency.
- **LogUtils**: Logging utility functions.
- **HostingUtils**: Helpers for host and configuration setup.
- **DictionaryUtils**: Utility methods for dictionary operations.
- **Utilities**: General-purpose helper methods.
- **DateExtensions**: Extension methods for date/time operations.

### 2. JSON and Protobuf Support
- **Conventions**: Provides default JSON serialization options.
- **JsonUtils**: Custom JSON converters (e.g., IntToBoolConverter, StringToUlongConverter, IntListToBoolListConverter).
- **ProtosUtils**: Utilities for working with Protobuf-generated code.

### 3. Models and Constants
- **Models/Enums.cs**: Shared enums and model definitions.
- **Constants**: Common constant values used throughout the solution.
- **Results**: Standard result and error handling types.

### 4. Metrics
- **MetricsRecorder**: Utility for recording and reporting application metrics.

---

This project is intended to be referenced by other projects in the solution to ensure consistency and reduce code duplication.
