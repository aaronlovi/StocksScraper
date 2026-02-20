using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace EDGARScraper.Services;

internal sealed class RateLimitedHttpClient : IDisposable {
    private const string UserAgent = "EDGARScraper (inno.and.logic@gmail.com)";
    private const int MaxRetries = 3;
    private const int DefaultRetryAfterSeconds = 10;
    private const int MaxRetryAfterSeconds = 120;

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _delayMilliseconds;
    private readonly ILogger _logger;

    internal RateLimitedHttpClient(ILogger logger, int maxConcurrent = 1, int delayMilliseconds = 110) {
        _logger = logger;
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        _delayMilliseconds = delayMilliseconds;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    internal async Task<Result<string>> FetchStringAsync(string url, CancellationToken ct) {
        await _semaphore.WaitAsync(ct);
        try {
            return await FetchWithRetryAsync(url, ct);
        } finally {
            await Task.Delay(_delayMilliseconds, CancellationToken.None);
            _semaphore.Release();
        }
    }

    private async Task<Result<string>> FetchWithRetryAsync(string url, CancellationToken ct) {
        for (int attempt = 0; attempt <= MaxRetries; attempt++) {
            try {
                HttpResponseMessage response = await _httpClient.GetAsync(url, ct);

                if (response.IsSuccessStatusCode) {
                    string content = await response.Content.ReadAsStringAsync(ct);
                    return Result<string>.Success(content);
                }

                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden
                        or HttpStatusCode.ServiceUnavailable) {
                    if (attempt == MaxRetries) {
                        string errMsg = $"HTTP {(int)response.StatusCode} for {url} after {MaxRetries} retries";
                        _logger.LogWarning("RateLimitedHttpClient - {Error}", errMsg);
                        return Result<string>.Failure(ErrorCodes.GenericError, errMsg);
                    }

                    int retryAfterSeconds = GetRetryAfterSeconds(response.Headers.RetryAfter);
                    _logger.LogWarning(
                        "RateLimitedHttpClient - HTTP {StatusCode} for {Url}, retrying in {Seconds}s (attempt {Attempt}/{MaxRetries})",
                        (int)response.StatusCode, url, retryAfterSeconds, attempt + 1, MaxRetries);
                    await Task.Delay(retryAfterSeconds * 1000, ct);
                    continue;
                }

                // Non-retryable error
                string failMsg = $"HTTP {(int)response.StatusCode} for {url}";
                _logger.LogWarning("RateLimitedHttpClient - {Error}", failMsg);
                return Result<string>.Failure(ErrorCodes.GenericError, failMsg);

            } catch (HttpRequestException ex) {
                if (attempt == MaxRetries) {
                    string errMsg = $"Failed to fetch {url} after {MaxRetries} retries: {ex.Message}";
                    _logger.LogWarning("RateLimitedHttpClient - {Error}", errMsg);
                    return Result<string>.Failure(ErrorCodes.GenericError, errMsg);
                }

                _logger.LogWarning(
                    "RateLimitedHttpClient - Request failed for {Url}: {Error}, retrying (attempt {Attempt}/{MaxRetries})",
                    url, ex.Message, attempt + 1, MaxRetries);
                await Task.Delay(DefaultRetryAfterSeconds * 1000, ct);

            } catch (TaskCanceledException) {
                return Result<string>.Failure(ErrorCodes.GenericError, $"Request cancelled for {url}");
            }
        }

        return Result<string>.Failure(ErrorCodes.GenericError, $"Exhausted retries for {url}");
    }

    private static int GetRetryAfterSeconds(RetryConditionHeaderValue? retryAfter) {
        if (retryAfter is null)
            return DefaultRetryAfterSeconds;

        if (retryAfter.Delta.HasValue) {
            int seconds = (int)retryAfter.Delta.Value.TotalSeconds;
            return Math.Clamp(seconds, 1, MaxRetryAfterSeconds);
        }

        if (retryAfter.Date.HasValue) {
            int seconds = (int)(retryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds;
            return Math.Clamp(seconds, 1, MaxRetryAfterSeconds);
        }

        return DefaultRetryAfterSeconds;
    }

    public void Dispose() {
        _httpClient.Dispose();
        _semaphore.Dispose();
    }
}
