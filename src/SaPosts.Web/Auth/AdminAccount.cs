using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SaPosts.Web.Auth;

/// <summary>Creates the admin account from configuration, or brings it in line with it.</summary>
internal static partial class AdminAccount
{
    public static async Task EnsureAsync(
        UserManager<IdentityUser> users, AdminOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        var email = options.Email?.Trim();
        var password = options.Password;
        var admin = await users.Users.SingleOrDefaultAsync(cancellationToken);

        if (admin is null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    "No admin account exists yet. Set Admin__Email and Admin__Password " +
                    "(in .env when using Docker Compose, see .env.example) and restart.");
            }

            admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            EnsureSucceeded(await users.CreateAsync(admin, password), "create the admin account");
            LogAdminCreated(logger);
            return;
        }

        if (!string.IsNullOrWhiteSpace(email) && !string.Equals(admin.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            EnsureSucceeded(await users.SetEmailAsync(admin, email), "update the admin email");
            EnsureSucceeded(await users.SetUserNameAsync(admin, email), "update the admin user name");
            LogAdminEmailUpdated(logger);
        }

        if (!string.IsNullOrEmpty(password) && !await users.CheckPasswordAsync(admin, password))
        {
            // Reset (not remove + add) so a password rejected by the validators leaves the old one in place.
            var token = await users.GeneratePasswordResetTokenAsync(admin);
            EnsureSucceeded(await users.ResetPasswordAsync(admin, token, password), "update the admin password");

            // Changing the password is also the way out of a lockout.
            await users.SetLockoutEndDateAsync(admin, null);
            await users.ResetAccessFailedCountAsync(admin);
            LogAdminPasswordUpdated(logger);
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Could not {action} from configuration: {errors}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin account created from configuration")]
    private static partial void LogAdminCreated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin email updated from configuration")]
    private static partial void LogAdminEmailUpdated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin password updated from configuration")]
    private static partial void LogAdminPasswordUpdated(ILogger logger);
}
