namespace PokeSharp.Engine.Debug.Common;

/// <summary>
///     Represents the result of an operation that can succeed or fail.
///     Provides explicit success/failure handling without exceptions.
/// </summary>
/// <typeparam name="T">The type of value returned on success.</typeparam>
public record Result<T>
{
    private Result() { }

    /// <summary>
    ///     Gets the value if the operation succeeded.
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    ///     Gets whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    ///     Gets whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    ///     Gets the error message if the operation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    ///     Gets the exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    ///     Creates a successful result with a value.
    /// </summary>
    public static Result<T> Success(T value)
    {
        return new Result<T> { IsSuccess = true, Value = value };
    }

    /// <summary>
    ///     Creates a failed result with an error message.
    /// </summary>
    public static Result<T> Failure(string error)
    {
        return new Result<T> { IsSuccess = false, Error = error };
    }

    /// <summary>
    ///     Creates a failed result with an error message and exception.
    /// </summary>
    public static Result<T> Failure(string error, Exception exception)
    {
        return new Result<T>
        {
            IsSuccess = false,
            Error = error,
            Exception = exception,
        };
    }

    /// <summary>
    ///     Creates a failed result from an exception.
    /// </summary>
    public static Result<T> Failure(Exception exception)
    {
        return new Result<T>
        {
            IsSuccess = false,
            Error = exception.Message,
            Exception = exception,
        };
    }
}

/// <summary>
///     Represents the result of an operation without a return value.
/// </summary>
public record Result
{
    private Result() { }

    /// <summary>
    ///     Gets whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    ///     Gets whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    ///     Gets the error message if the operation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    ///     Gets the exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    public static Result Success()
    {
        return new Result { IsSuccess = true };
    }

    /// <summary>
    ///     Creates a failed result with an error message.
    /// </summary>
    public static Result Failure(string error)
    {
        return new Result { IsSuccess = false, Error = error };
    }

    /// <summary>
    ///     Creates a failed result with an error message and exception.
    /// </summary>
    public static Result Failure(string error, Exception exception)
    {
        return new Result
        {
            IsSuccess = false,
            Error = error,
            Exception = exception,
        };
    }

    /// <summary>
    ///     Creates a failed result from an exception.
    /// </summary>
    public static Result Failure(Exception exception)
    {
        return new Result
        {
            IsSuccess = false,
            Error = exception.Message,
            Exception = exception,
        };
    }
}
