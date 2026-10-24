using System.Net;

namespace SaPosts.IntegrationTests;

public sealed class AdminAuthTests(BlogFactory factory) : IClassFixture<BlogFactory>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_pages_redirect_anonymous_visitors_to_the_login_page()
    {
        var client = factory.CreateClientWithoutRedirects();

        var response = await client.GetAsync(new Uri("/admin", UriKind.Relative), CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/login?ReturnUrl=%2Fadmin", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_can_sign_in_and_out()
    {
        var client = factory.CreateClientWithoutRedirects();

        var login = await AdminSession.LogInAsync(client, BlogFactory.AdminEmail, BlogFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/admin/posts", login.Headers.Location!.OriginalString);

        var dashboard = await client.GetAsync(new Uri("/admin/posts", UriKind.Relative), CancellationToken);
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.Contains("Nouvel article", await dashboard.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
        Assert.True(dashboard.Headers.CacheControl?.NoStore);

        var logout = await AdminSession.LogOutAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var afterLogout = await client.GetAsync(new Uri("/admin", UriKind.Relative), CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    public async Task Login_ignores_non_local_return_urls(string returnUrl)
    {
        var client = factory.CreateClientWithoutRedirects();

        var response = await AdminSession.LogInAsync(client, BlogFactory.AdminEmail, BlogFactory.AdminPassword, returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/posts", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account()
    {
        await using var isolated = new BlogFactory();
        var client = isolated.CreateClientWithoutRedirects();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await AdminSession.LogInAsync(client, BlogFactory.AdminEmail, "wrong password, try again");
            Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        }

        var withCorrectPassword = await AdminSession.LogInAsync(client, BlogFactory.AdminEmail, BlogFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, withCorrectPassword.StatusCode);
        Assert.Contains("Réessayez dans 15 minutes", await withCorrectPassword.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_attempts_are_rate_limited()
    {
        await using var isolated = new BlogFactory();
        var client = isolated.CreateClientWithoutRedirects();

        // Unknown emails never lock an account, which is what the rate limit is for.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var allowed = await AdminSession.LogInAsync(client, $"guess{attempt}@example.test", "some password guess");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var limited = await AdminSession.LogInAsync(client, "guess@example.test", "some password guess");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }
}
