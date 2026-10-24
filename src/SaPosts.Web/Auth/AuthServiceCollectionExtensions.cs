using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

using SaPosts.Web.Data;

namespace SaPosts.Web.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services)
    {
        services.AddOptions<AdminOptions>().BindConfiguration(AdminOptions.SectionName);

        // Identity core only: no Identity UI package, hence no registration or account pages at all.
        services.AddIdentityCore<IdentityUser>(options =>
            {
                // Length over composition rules (NIST SP 800-63B).
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<BlogDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "saposts.auth";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.LoginPath = "/admin/login";
            options.LogoutPath = "/admin/logout";
            options.AccessDeniedPath = "/admin/login";
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.SlidingExpiration = true;
        });
        services.AddAntiforgery(options => options.Cookie.Name = "saposts.antiforgery");

        // Lockout only protects the existing account; this also limits guessing with other
        // emails and the cost of password hashing. Partitioned by client IP, so production needs
        // forwarded headers from the reverse proxy, or every visitor shares the proxy's IP.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                response.ContentType = "text/plain; charset=utf-8";
                return new ValueTask(response.WriteAsync("Trop de tentatives. Réessayez dans quelques minutes.", cancellationToken));
            };
            options.AddPolicy(RateLimitPolicies.Login, context =>
                HttpMethods.IsPost(context.Request.Method)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(15) })
                    : RateLimitPartition.GetNoLimiter(string.Empty));
        });

        return services;
    }
}
