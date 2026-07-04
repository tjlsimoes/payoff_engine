namespace PayoffEngine;

public static class AuthExtensions
{
    private static readonly string[] ExemptPathPrefixes = ["/health", "/openapi", "/scalar"];

    public static void UseApiKeyAuth(this WebApplication app)
    {
        var configuredKey = app.Configuration["ApiKey"]
            ?? throw new InvalidOperationException("ApiKey configuration value is required.");

        app.Use(async (context, next) =>
        {
            if (ExemptPathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
            {
                await next();
                return;
            }

            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var providedKey) ||
                providedKey != configuredKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing or invalid API key.");
                return;
            }

            await next();
        });
    }
}
