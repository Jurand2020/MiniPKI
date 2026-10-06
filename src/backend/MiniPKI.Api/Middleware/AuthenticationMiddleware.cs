using MiniPKI.Api.Auth;

namespace MiniPKI.Api.Middleware;

/// <summary>
/// Validates session cookie for protected API endpoints.
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/status",
        "/api/auth/set-password",
        "/api/ca/chain",
        "/api/ca/root",
        "/api/ca/intermediate",
        "/api/crl/current",
        "/health"
    };

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SessionStore sessions)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Allow public paths
        if (PublicPaths.Contains(path) || !path.StartsWith("/api"))
        {
            await _next(context);
            return;
        }

        // Check session cookie
        var token = context.Request.Cookies["minipki_session"];
        if (!sessions.IsValid(token))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await _next(context);
    }
}
