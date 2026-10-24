using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace SaPosts.IntegrationTests;

/// <summary>Runs the app on its own temporary data directory (SQLite database and Data Protection keys).</summary>
public sealed class BlogFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@example.test";
    public const string AdminPassword = "correct horse battery staple";

    private readonly string? sharedDataPath;
    private readonly string ownDataPath = Path.Combine(Path.GetTempPath(), "saposts-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Set to another factory's path to simulate a restart on existing data; that factory keeps ownership.</summary>
    public string DataPath
    {
        get => sharedDataPath ?? ownDataPath;
        init => sharedDataPath = value;
    }

    public string? ConfiguredAdminEmail { get; init; } = AdminEmail;

    public string? ConfiguredAdminPassword { get; init; } = AdminPassword;

    public HttpClient CreateClientWithoutRedirects() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:DataPath", DataPath);
        builder.UseSetting("Admin:Email", ConfiguredAdminEmail);
        builder.UseSetting("Admin:Password", ConfiguredAdminPassword);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (sharedDataPath is null && Directory.Exists(ownDataPath))
        {
            // Pooled connections keep the database file open, which blocks deletion on Windows.
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(ownDataPath, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: it is a temp directory.
            }
        }
    }
}
