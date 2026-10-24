namespace SaPosts.Web.Infrastructure.Security;

public static class SecurityHeaders
{
    // Visitors' browsers only ever load resources from this origin, so the policy denies by default
    // and allows 'self' per resource type. No 'unsafe-inline': scripts and styles must live in files.
    internal const string ContentSecurityPolicy =
        "default-src 'none'; " +
        "script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; media-src 'self'; connect-src 'self'; " +
        "form-action 'self'; base-uri 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(static (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            if (context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
            {
                // Authenticated pages: never stored by browsers or proxies, never indexed. Applied when the
                // response starts, after antiforgery has set (and would otherwise warn about) cache headers.
                headers["X-Robots-Tag"] = "noindex";
                context.Response.OnStarting(static state =>
                {
                    ((HttpResponse)state).Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                }, context.Response);
            }

            return next(context);
        });
}
