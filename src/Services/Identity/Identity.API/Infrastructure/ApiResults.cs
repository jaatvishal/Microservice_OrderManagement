using BuildingBlocks.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Infrastructure;

public static class ApiResults
{
    public static IResult FromError(Error error)
    {
        var (status, title) = error.Code switch
        {
            "Identity.Validation" => (StatusCodes.Status400BadRequest, "Validation failed."),
            "Identity.EmailConflict" => (StatusCodes.Status409Conflict, "Conflict."),
            "Identity.Unauthorized" => (StatusCodes.Status401Unauthorized, "Unauthorized."),
            "Identity.Forbidden" => (StatusCodes.Status403Forbidden, "Forbidden."),
            "Identity.NotFound" => (StatusCodes.Status404NotFound, "Not found."),
            _ => (StatusCodes.Status400BadRequest, "Request failed.")
        };

        return Results.Problem(
            title: title,
            detail: error.Message,
            statusCode: status,
            type: $"https://httpstatuses.com/{status}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code
            });
    }

    public static ProblemDetails UnauthorizedBody() => new()
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "Unauthorized.",
        Detail = "A valid access token is required.",
        Type = "https://httpstatuses.com/401"
    };

    public static ProblemDetails ForbiddenBody() => new()
    {
        Status = StatusCodes.Status403Forbidden,
        Title = "Forbidden.",
        Detail = "You do not have access to this resource.",
        Type = "https://httpstatuses.com/403"
    };
}
