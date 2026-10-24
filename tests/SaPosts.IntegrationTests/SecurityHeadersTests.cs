using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SaPosts.IntegrationTests;

public sealed class SecurityHeadersTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Pages_send_a_deny_by_default_content_security_policy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.StartsWith("default-src 'none'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Static_files_get_the_same_headers()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/css/tokens.css", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task Https_responses_outside_development_send_hsts()
    {
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        // HSTS is skipped for localhost, so use a real-looking host name.
        var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://blog.test") });

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var hsts = Assert.Single(response.Headers.GetValues("Strict-Transport-Security"));
        Assert.Equal("max-age=31536000", hsts);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var client = factory.CreateClient();

        var body = await client.GetStringAsync(new Uri("/healthz", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("Healthy", body);
    }
}
