namespace McpGateway.Application;

/// <summary>Failure category of an application operation; the API layer maps these to HTTP statuses.</summary>
public enum OperationError
{
    None,
    Validation,
    NotFound,
    Conflict,
}

/// <summary>
/// Typed outcome of an application operation. Domain exceptions are caught at
/// the service boundary and converted into these so endpoints contain no
/// business rules and no exception-based control flow.
/// </summary>
public sealed record OperationResult<T>
{
    public bool IsSuccess => Error == OperationError.None;
    public T? Value { get; }
    public OperationError Error { get; }
    public string? Message { get; }

    private OperationResult(T? value, OperationError error, string? message)
    {
        Value = value;
        Error = error;
        Message = message;
    }

    public static OperationResult<T> Success(T value) => new(value, OperationError.None, null);
    public static OperationResult<T> Invalid(string message) => new(default, OperationError.Validation, message);
    public static OperationResult<T> NotFound(string message) => new(default, OperationError.NotFound, message);
    public static OperationResult<T> Conflict(string message) => new(default, OperationError.Conflict, message);
}
