using SaPosts.Web.Posts;

namespace SaPosts.UnitTests;

public sealed class MarkdownRendererTests
{
    private readonly MarkdownRenderer renderer = new();

    [Fact]
    public void Renders_common_markdown()
    {
        var html = renderer.Render("Du **gras**, de l’*italique*, du `code` et un [lien](https://example.com).");

        Assert.Equal(
            "<p>Du <strong>gras</strong>, de l’<em>italique</em>, du <code>code</code> et un <a href=\"https://example.com\">lien</a>.</p>",
            html.Trim());
    }

    [Fact]
    public void Raw_html_is_shown_as_text_not_executed()
    {
        var html = renderer.Render("Avant <script>alert(1)</script> <img src=x onerror=alert(1)> après");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[clic](javascript:alert(1))")]
    [InlineData("[clic](JAVASCRIPT:alert(1))")]
    [InlineData("[clic](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[clic](vbscript:msgbox)")]
    public void Dangerous_link_schemes_are_removed(string markdown)
    {
        var html = renderer.Render(markdown);

        Assert.DoesNotContain("href", html, StringComparison.Ordinal);
        Assert.Contains("clic", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Mailto_and_relative_links_are_kept()
    {
        var html = renderer.Render("[écrire](mailto:moi@example.com) et [contact](/contact)");

        Assert.Contains("href=\"mailto:moi@example.com\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/contact\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_one_heading_becomes_h2_and_other_levels_are_kept()
    {
        var html = renderer.Render("# Partie\n\n## Section\n\n### Détail");

        Assert.DoesNotContain("<h1", html, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"partie\">Partie</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"section\">Section</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"détail\">Détail</h3>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Images_load_lazily()
    {
        var html = renderer.Render("![Un chat](/media/chat.jpg)");

        Assert.Contains("alt=\"Un chat\"", html, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", html, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_has_no_inline_styles_which_the_csp_would_block()
    {
        // Pipe tables express column alignment with style attributes.
        var html = renderer.Render("| Nom | Valeur |\n|:----|------:|\n| a | 1 |");

        Assert.Contains("<table", html, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrollable_blocks_are_keyboard_focusable()
    {
        var html = renderer.Render("```csharp\nvar x = 1;\n```\n\n| a |\n|---|\n| 1 |");

        Assert.Contains("<pre tabindex=\"0\">", html, StringComparison.Ordinal);
        Assert.Contains("<table tabindex=\"0\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Footnotes_are_rendered()
    {
        var html = renderer.Render("Une affirmation[^1].\n\n[^1]: La source.");

        Assert.Contains("class=\"footnote-ref\"", html, StringComparison.Ordinal);
        Assert.Contains("La source.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_input_gives_empty_output() => Assert.Equal(string.Empty, renderer.Render(null).Trim());
}
