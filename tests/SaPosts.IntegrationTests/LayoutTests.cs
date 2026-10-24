using System.Text.RegularExpressions;

namespace SaPosts.IntegrationTests;

public sealed partial class LayoutTests(BlogFactory factory)
    : IClassFixture<BlogFactory>
{
    [Fact]
    public async Task Font_preload_points_at_the_file_the_stylesheet_uses()
    {
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), cancellationToken);
        var tokens = await client.GetStringAsync(new Uri("/css/tokens.css", UriKind.Relative), cancellationToken);

        var preload = FontPreload().Match(html).Groups["href"].Value;
        Assert.StartsWith("/fonts/", preload, StringComparison.Ordinal);
        // tokens.css lives in /css/, so its font URLs are relative to it.
        Assert.Contains($"url(\"..{preload}\")", tokens, StringComparison.Ordinal);
    }

    [GeneratedRegex("""<link rel="preload" href="(?<href>[^"]+)" as="font""")]
    private static partial Regex FontPreload();
}
