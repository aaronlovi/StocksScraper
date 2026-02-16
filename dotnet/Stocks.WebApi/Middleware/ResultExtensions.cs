using Microsoft.AspNetCore.Http;
using Stocks.Shared;
using Stocks.Shared.Models;

using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Stocks.WebApi.Middleware;

public static class ResultExtensions {
    public static IResult ToHttpResult<T>(this Result<T> result) {
        if (result.IsSuccess)
            return Results.Ok(result.Value);
        if (result.ErrorCode == ErrorCodes.NotFound)
            return Results.NotFound(new { error = result.ErrorMessage });
        return Results.Problem(result.ErrorMessage ?? "An error occurred");
    }
}
