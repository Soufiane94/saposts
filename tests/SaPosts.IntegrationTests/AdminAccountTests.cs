using System.Net;

namespace SaPosts.IntegrationTests;

public sealed class AdminAccountTests
{
    [Fact]
    public async Task Startup_fails_clearly_when_no_admin_exists_and_none_is_configured()
    {
        await using var factory = new BlogFactory { ConfiguredAdminPassword = null };

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Admin__Password", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_password_in_configuration_replaces_the_stored_one_on_restart()
    {
        const string newPassword = "a brand new passphrase";
        await using var first = new BlogFactory();
        _ = first.Server; // first start: creates the admin with the original password

        await using var restarted = new BlogFactory { DataPath = first.DataPath, ConfiguredAdminPassword = newPassword };
        var client = restarted.CreateClientWithoutRedirects();

        var withOldPassword = await AdminSession.LogInAsync(client, BlogFactory.AdminEmail, BlogFactory.AdminPassword);
        var withNewPassword = await AdminSession.LogInAsync(client, BlogFactory.AdminEmail, newPassword);

        Assert.Equal(HttpStatusCode.OK, withOldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, withNewPassword.StatusCode);
    }

    [Fact]
    public async Task The_admin_can_remove_secrets_from_configuration_after_the_first_start()
    {
        await using var first = new BlogFactory();
        _ = first.Server;

        await using var restarted = new BlogFactory { DataPath = first.DataPath, ConfiguredAdminEmail = null, ConfiguredAdminPassword = null };
        var client = restarted.CreateClientWithoutRedirects();

        var login = await AdminSession.LogInAsync(client, BlogFactory.AdminEmail, BlogFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }
}
