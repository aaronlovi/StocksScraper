namespace Utilities;

public record Results
{
    public static readonly Results Success = new(true);

    public Results(bool success, string errorMessage = "")
    {
        IsSuccess = success;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public bool IsError => !IsSuccess;
    public string ErrorMessage { get; }

    public static Results SuccessResult() => new(true);

    public static Results FailureResult(string errorMessage) => new(false, errorMessage);
}

public record GenericResults<T>
{
    public GenericResults(bool success, string errorMessage = "", T? data = default)
    {
        IsSuccess = success;
        ErrorMessage = errorMessage;
        Data = data;
    }

    public bool IsSuccess { get; }
    public bool IsError => !IsSuccess;
    public string ErrorMessage { get; }
    public T? Data { get; }

    public static GenericResults<T> SuccessResult(T data) => new(true, string.Empty, data);

    public static GenericResults<T> FailureResult(string errorMessage) => new(false, errorMessage, default);
}
