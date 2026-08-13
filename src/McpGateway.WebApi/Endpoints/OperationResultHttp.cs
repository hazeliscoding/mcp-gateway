using McpGateway.Application;

namespace McpGateway.WebApi.Endpoints;

/// <summary>Single place where application outcomes become HTTP statuses.</summary>
public static class OperationResultHttp
{
    public static IResult ToHttp<T>(this OperationResult<T> result, Func<T, IResult> onSuccess) =>
        result.Error switch
        {
            OperationError.None => onSuccess(result.Value!),
            OperationError.Validation => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed", detail: result.Message),
            OperationError.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Not found", detail: result.Message),
            OperationError.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Conflict", detail: result.Message),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
}
