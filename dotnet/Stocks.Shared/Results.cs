using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Stocks.Shared.Models;

namespace Stocks.Shared;

/// <summary>
/// Represents the result of an operation, containing error information if unsuccessful.
/// </summary>
public interface IResult {
    ErrorCodes ErrorCode { get; }
    bool IsSuccess { get; }
    bool IsFailure { get; }
    string ErrorMessage { get; }
    IReadOnlyCollection<string> ErrorParams { get; }
}

/// <summary>
/// Represents a non-generic result of an operation, with error information if unsuccessful.
/// </summary>
public record Result : IResult {
    public static readonly Result Success = new(ErrorCodes.None);

    public Result(ErrorCodes errorCode, string errorMessage = "", IReadOnlyCollection<string>? errorParams = null) {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        ErrorParams = errorParams ?? [];
    }

    [JsonPropertyName("errorCode")] public ErrorCodes ErrorCode { get; init; }
    [JsonPropertyName("errorMessage")] public string ErrorMessage { get; init; }
    [JsonPropertyName("errorParams")] public IReadOnlyCollection<string> ErrorParams { get; init; }
    [JsonIgnore] public bool IsSuccess => ErrorCode is ErrorCodes.None;
    [JsonIgnore] public bool IsFailure => !IsSuccess;

    public static Result Failure(ErrorCodes errorCode, string errMsg = "", params string[] errorParams)
        => new(errorCode, errMsg, errorParams.Length == 0 ? null : errorParams);
    public static Result Failure(IResult res) => new(res.ErrorCode, res.ErrorMessage, res.ErrorParams);
}

/// <summary>
/// Represents a generic result of an operation, containing a value if successful, or error information if unsuccessful.
/// </summary>
public record Result<T> : IResult {
    public Result(ErrorCodes errorCode, string errorMessage = "", IReadOnlyCollection<string>? errorParams = null, T? value = default) {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        ErrorParams = errorParams ?? [];
        Value = value;
    }

    [JsonPropertyName("errorCode")] public ErrorCodes ErrorCode { get; init; }
    [JsonPropertyName("errorMessage")] public string ErrorMessage { get; init; }
    [JsonPropertyName("errorParams")] public IReadOnlyCollection<string> ErrorParams { get; init; }
    [JsonPropertyName("value")] public T? Value { get; init; }
    [JsonIgnore] public bool IsSuccess => ErrorCode is ErrorCodes.None;
    [JsonIgnore] public bool IsFailure => !IsSuccess;

    public static Result<T> Success(T value) => new(ErrorCodes.None, "", null, value);
    public static Result<T> Failure(ErrorCodes errorCode, string errMsg = "", params string[] errorParams)
        => new(errorCode, errMsg, errorParams.Length == 0 ? null : errorParams);
    public static Result<T> Failure(IResult res) => new(res.ErrorCode, res.ErrorMessage, res.ErrorParams);
}

public static class ResultFluentExtensions {
    public static Result Then(this Result result, Func<Result> fn) => ThenCore(result, fn);
    public static Result Then(this Result result, Func<Result, Result> fn) => ThenCore(result, fn);
    public static Task<Result> Then(this Result result, Func<Task<Result>> fn) => ThenCoreAsync(result, fn);
    public static Task<Result> Then(this Result result, Func<Result, Task<Result>> fn) => ThenCoreAsync(result, fn);
    public static async Task<Result> Then(this Task<Result> resultAsTask, Func<Result> fn) => ThenCore(await resultAsTask, fn);
    public static async Task<Result> Then(this Task<Result> resultAsTask, Func<Result, Result> fn) => ThenCore(await resultAsTask, fn);
    public static async Task<Result> Then(this Task<Result> resultAsTask, Func<Task<Result>> fn) => await ThenCoreAsync(await resultAsTask, fn);
    public static async Task<Result> Then(this Task<Result> resultAsTask, Func<Result, Task<Result>> fn) => await ThenCoreAsync(await resultAsTask, fn);

    public static Result OnCompletion(this Result result, Action<Result>? onSuccess = null, Action<Result>? onFailure = null)
        => OnCompletionCore(result, onSuccess, onFailure);

    public static async Task<Result> OnCompletion(this Task<Result> resultAsTask, Action<Result>? onSuccess = null, Action<Result>? onFailure = null)
        => OnCompletionCore(await resultAsTask, onSuccess, onFailure);

    public static Result OnSuccess(this Result result, Action<Result> onSuccess) => OnCompletion(result, onSuccess, null);
    public static Task<Result> OnSuccess(this Task<Result> resultAsTask, Action<Result> onSuccess) => OnCompletion(resultAsTask, onSuccess, null);

    public static Result OnFailure(this Result result, Action<Result> onFailure) => OnCompletion(result, null, onFailure);
    public static Task<Result> OnFailure(this Task<Result> resultAsTask, Action<Result> onFailure) => OnCompletion(resultAsTask, null, onFailure);

    #region PRIVATE HELPER METHODS

    private static Result ThenCore(Result result, Func<Result> fn) {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(fn);
        return result.IsSuccess ? fn() : result;
    }

    private static Result ThenCore(Result result, Func<Result, Result> fn) {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(fn);
        return result.IsSuccess ? fn(result) : result;
    }

    private static async Task<Result> ThenCoreAsync(Result result, Func<Task<Result>> fn) {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(fn);
        return result.IsSuccess ? await fn() : result;
    }

    private static async Task<Result> ThenCoreAsync(Result result, Func<Result, Task<Result>> fn) {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(fn);
        return result.IsSuccess ? await fn(result) : result;
    }

    private static Result OnCompletionCore(Result result, Action<Result>? onSuccess, Action<Result>? onFailure) {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
            onSuccess?.Invoke(result);
        else
            onFailure?.Invoke(result);
        return result;
    }

    #endregion
}
