using Microsoft.AspNetCore.Mvc.Filters;

namespace Identity.API.Infrastructure;

/// <summary>
/// Access and refresh tokens must not be retained by a shared cache or the browser disk cache.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoStoreAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        base.OnActionExecuting(context);
    }
}
