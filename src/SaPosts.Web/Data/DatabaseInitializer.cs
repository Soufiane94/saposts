using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SaPosts.Web.Auth;
using SaPosts.Web.Posts;

namespace SaPosts.Web.Data;

public static class DatabaseInitializer
{
    /// <summary>Applies migrations and aligns the admin account with configuration, before requests are served.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
        Directory.CreateDirectory(storage.DataPath);
        Directory.CreateDirectory(storage.KeysDirectory);

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var db = services.GetRequiredService<BlogDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        // Readers are not blocked while the admin writes. The mode is persisted in the database file.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

        await AdminAccount.EnsureAsync(
            services.GetRequiredService<UserManager<IdentityUser>>(),
            services.GetRequiredService<IOptions<AdminOptions>>().Value,
            services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdminAccount)),
            cancellationToken);

        await services.GetRequiredService<PostEditor>().RerenderOutdatedAsync(cancellationToken);
    }
}
