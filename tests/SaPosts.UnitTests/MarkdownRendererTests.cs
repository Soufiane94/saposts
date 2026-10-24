using SaPosts.Web.Media;
using SaPosts.Web.Posts;

namespace SaPosts.UnitTests;

public sealed class MarkdownRendererTests
{
    private static readonly Guid PhotoId = Guid.Parse("0192d8f0-0000-7000-8000-000000000001");
    private static readonly Guid ClipId = Guid.Parse("0192d8f0-0000-7000-8000-000000000002");
    private static readonly Guid PosterId = Guid.Parse("0192d8f0-0000-7000-8000-000000000003");

    private static readonly MediaItem Poster = new()
    {
        Id = PosterId,
        Kind = MediaKind.Image,
        Width = 800,
        Height = 450,
        Widths = [400, 800],
        AltText = "Affiche",
    };

    private readonly FakeCatalog catalog = new(
        new MediaItem { Id = PhotoId, Kind = MediaKind.Image, Width = 1600, Height = 1000, Widths = [400, 800, 1200, 1600], AltText = "Le port au matin" },
        new MediaItem { Id = ClipId, Kind = MediaKind.Video, ContentType = "video/mp4", Width = 1280, Height = 720, PosterId = PosterId, Poster = Poster },
        Poster);

    private Task<string> RenderAsync(string? markdown) =>
        new MarkdownRenderer(catalog).RenderAsync(markdown, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Renders_common_markdown()
    {
        var html = await RenderAsync("Du **gras**, de l’*italique*, du `code` et un [lien](https://example.com).");

        Assert.Equal(
            "<p>Du <strong>gras</strong>, de l’<em>italique</em>, du <code>code</code> et un <a href=\"https://example.com\">lien</a>.</p>",
            html.Trim());
    }

    [Fact]
    public async Task Raw_html_is_shown_as_text_not_executed()
    {
        var html = await RenderAsync("Avant <script>alert(1)</script> <img src=x onerror=alert(1)> après");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[clic](javascript:alert(1))")]
    [InlineData("[clic](JAVASCRIPT:alert(1))")]
    [InlineData("[clic](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[clic](vbscript:msgbox)")]
    public async Task Dangerous_link_schemes_are_removed(string markdown)
    {
        var html = await RenderAsync(markdown);

        Assert.DoesNotContain("href", html, StringComparison.Ordinal);
        Assert.Contains("clic", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mailto_and_relative_links_are_kept()
    {
        var html = await RenderAsync("[écrire](mailto:moi@example.com) et [contact](/contact)");

        Assert.Contains("href=\"mailto:moi@example.com\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/contact\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_level_one_heading_becomes_h2_and_other_levels_are_kept()
    {
        var html = await RenderAsync("# Partie\n\n## Section\n\n### Détail");

        Assert.DoesNotContain("<h1", html, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"partie\">Partie</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"section\">Section</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"détail\">Détail</h3>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Output_has_no_inline_styles_which_the_csp_would_block()
    {
        // Pipe tables express column alignment with style attributes.
        var html = await RenderAsync("| Nom | Valeur |\n|:----|------:|\n| a | 1 |");

        Assert.Contains("<table", html, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scrollable_blocks_are_keyboard_focusable()
    {
        var html = await RenderAsync("```csharp\nvar x = 1;\n```\n\n| a |\n|---|\n| 1 |");

        Assert.Contains("<pre tabindex=\"0\">", html, StringComparison.Ordinal);
        Assert.Contains("<table tabindex=\"0\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Footnotes_are_rendered()
    {
        var html = await RenderAsync("Une affirmation[^1].\n\n[^1]: La source.");

        Assert.Contains("class=\"footnote-ref\"", html, StringComparison.Ordinal);
        Assert.Contains("La source.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_input_gives_empty_output() => Assert.Equal(string.Empty, (await RenderAsync(null)).Trim());

    [Fact]
    public async Task A_library_image_becomes_a_responsive_picture()
    {
        var html = await RenderAsync($"![Bateaux à quai](/media/{PhotoId})");

        Assert.Contains("<picture>", html, StringComparison.Ordinal);
        Assert.Contains($"<source type=\"image/avif\" srcset=\"/media/{PhotoId}/400.avif 400w, /media/{PhotoId}/800.avif 800w, /media/{PhotoId}/1200.avif 1200w, /media/{PhotoId}/1600.avif 1600w\"", html, StringComparison.Ordinal);
        Assert.Contains("<source type=\"image/webp\"", html, StringComparison.Ordinal);
        Assert.Contains($"src=\"/media/{PhotoId}/1200.jpg\"", html, StringComparison.Ordinal);
        Assert.Contains("width=\"1600\" height=\"1000\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Bateaux à quai\"", html, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_markdown_alt_text_falls_back_to_the_library_one()
    {
        var html = await RenderAsync($"![](/media/{PhotoId})");

        Assert.Contains("alt=\"Le port au matin\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Animated_and_transparent_images_get_suitable_formats()
    {
        var animatedId = Guid.NewGuid();
        var logoId = Guid.NewGuid();
        catalog.Add(new MediaItem { Id = animatedId, Kind = MediaKind.Image, Width = 320, Height = 240, Widths = [320], IsAnimated = true, AltText = "Animation" });
        catalog.Add(new MediaItem { Id = logoId, Kind = MediaKind.Image, Width = 400, Height = 400, Widths = [400], HasAlpha = true, AltText = "Logo" });

        var html = await RenderAsync($"![](/media/{animatedId})\n\n![](/media/{logoId})");

        Assert.DoesNotContain($"/media/{animatedId}/320.avif", html, StringComparison.Ordinal);
        Assert.Contains($"src=\"/media/{animatedId}/320.gif\"", html, StringComparison.Ordinal);
        Assert.Contains($"src=\"/media/{logoId}/400.png\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_library_video_becomes_a_native_video_element()
    {
        var html = await RenderAsync($"![Démonstration](/media/{ClipId})");

        Assert.Contains("<video controls=\"\" preload=\"metadata\" playsinline=\"\" width=\"1280\" height=\"720\"", html, StringComparison.Ordinal);
        Assert.Contains($"poster=\"/media/{PosterId}/800.webp\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Démonstration\"", html, StringComparison.Ordinal);
        Assert.Contains($"<source src=\"/media/{ClipId}/original.mp4\" type=\"video/mp4\">", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("![Ailleurs](https://example.com/chat.jpg)")]
    [InlineData("![Ailleurs](//example.com/chat.jpg)")]
    [InlineData("![Ailleurs](/images/chat.jpg)")]
    [InlineData("![Inconnu](/media/0192d8f0-0000-7000-8000-0000000000ff)")]
    public async Task Images_outside_the_library_are_dropped(string markdown)
    {
        var html = await RenderAsync($"Avant {markdown} après");

        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", html, StringComparison.Ordinal);
        Assert.Contains("Avant", html, StringComparison.Ordinal);
    }

    private sealed class FakeCatalog : IMediaCatalog
    {
        private readonly Dictionary<Guid, MediaItem> items;

        public FakeCatalog(params MediaItem[] items) => this.items = items.ToDictionary(m => m.Id);

        public void Add(MediaItem item) => items[item.Id] = item;

        public Task<IReadOnlyDictionary<Guid, MediaItem>> FindAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, MediaItem>>(items.Where(pair => ids.Contains(pair.Key)).ToDictionary());
    }
}
