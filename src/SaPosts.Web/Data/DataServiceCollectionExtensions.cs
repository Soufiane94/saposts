using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SaPosts.Web.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddBlogData(this IServiceCollection services)
    {
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart()
            .PostConfigure<IHostEnvironment>((options, environment) =>
                options.DataPath = Path.GetFullPath(options.DataPath, environment.ContentRootPath));

        // Paths are resolved from options at first use (not read from configuration here),
        // so test hosts can point each instance at its own directory.
        services.AddDbContext<BlogDbContext>((provider, options) =>
        {
            var storage = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            options.UseSqlite(new SqliteConnectionStringBuilder { DataSource = storage.DatabaseFile }.ToString());
        });

        // Keys must survive container restarts, otherwise every redeploy signs the admin out
        // and invalidates antiforgery tokens on open forms. Same as PersistKeysToFileSystem, resolved lazily.
        services.AddDataProtection().SetApplicationName("SaPosts");
        services.AddOptions<KeyManagementOptions>()
            .Configure<IOptions<StorageOptions>, ILoggerFactory>((options, storage, loggerFactory) =>
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(storage.Value.KeysDirectory), loggerFactory));

        services.AddHealthChecks().AddDbContextCheck<BlogDbContext>();

        return services;
    }
}
