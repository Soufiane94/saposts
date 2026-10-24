using System.Globalization;

using AngleSharp.Dom;

using Ganss.Xss;

using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using SaPosts.Web.Media;

namespace SaPosts.Web.Posts;

/// <summary>
/// Markdown to safe HTML: Markdig with raw HTML disabled, library media expanded to responsive
/// markup, then HtmlSanitizer with an explicit allowlist. Serves the editor preview and stored bodies.
/// </summary>
public sealed class MarkdownRenderer(IMediaCatalog catalog)
{
    /// <summary>Bump when the output changes: posts rendered by an older version are re-rendered at startup.</summary>
    public const int Version = 2;

    // Pipelines are immutable and thread-safe. No MediaLinks (iframes) and no generic attributes ({style=…}).
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseFootnotes()
        .UseListExtras()
        .Build();

    private readonly HtmlSanitizer sanitizer = CreateSanitizer();

    public async Task<string> RenderAsync(string? markdown, CancellationToken cancellationToken)
    {
        var document = Markdown.Parse(markdown ?? string.Empty, Pipeline);

        // The post title is the page's only h1: sections start at "##", and a "#" heading becomes h2 as well.
        foreach (var heading in document.Descendants<HeadingBlock>().Where(h => h.Level == 1))
        {
            heading.Level = 2;
        }

        var mediaIds = document.Descendants<LinkInline>()
            .Where(link => link.IsImage)
            .Select(link => MediaFiles.ParseReference(link.Url))
            .OfType<Guid>()
            .ToHashSet();
        var media = await catalog.FindAsync(mediaIds, cancellationToken);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.ObjectRenderers.ReplaceOrAdd<LinkInlineRenderer>(new MediaLinkRenderer(media));
        renderer.Render(document);
        await writer.FlushAsync(cancellationToken);

        return sanitizer.Sanitize(writer.ToString());
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        // Starts from the defaults (which screen URI attributes against AllowedSchemes), then narrows them.
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith([
            "p", "h2", "h3", "h4", "h5", "h6", "a", "strong", "em", "del", "code", "pre", "blockquote",
            "ul", "ol", "li", "hr", "br", "img", "table", "thead", "tbody", "tr", "th", "td", "sup", "div",
            "picture", "source", "video",
        ]);

        // No "style": inline styles would be blocked by the CSP anyway (pipe-table alignment is dropped).
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith([
            "href", "src", "alt", "title", "id", "class", "start", "loading", "decoding", "tabindex",
            "srcset", "sizes", "type", "width", "height", "fetchpriority",
            "controls", "preload", "playsinline", "poster", "aria-label",
        ]);
        // srcset is already screened entry by entry (UriListAttributes default).
        sanitizer.UriAttributes.Add("poster");

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedAtRules.Clear();

        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement element)
            {
                return;
            }

            switch (element.LocalName)
            {
                case "img" or "source" or "video" when LoadsRemoteResource(element):
                    // Readers' browsers must never fetch third-party resources.
                    element.Remove();
                    break;
                case "img":
                    element.SetAttribute("loading", "lazy");
                    element.SetAttribute("decoding", "async");
                    break;
                case "pre" or "table":
                    // These scroll horizontally on small screens; keyboard users must be able to reach them.
                    element.SetAttribute("tabindex", "0");
                    break;
            }
        };

        return sanitizer;
    }

    private static readonly string[] ResourceAttributes = ["src", "srcset", "poster"];

    private static bool LoadsRemoteResource(IElement element) =>
        ResourceAttributes
            .Select(element.GetAttribute)
            .Any(value => value is not null && (value.Contains("//", StringComparison.Ordinal) || value.Contains(':', StringComparison.Ordinal)));

    /// <summary>Images that point to the media library become &lt;picture&gt; or &lt;video&gt;; any other image is dropped.</summary>
    private sealed class MediaLinkRenderer(IReadOnlyDictionary<Guid, MediaItem> media) : LinkInlineRenderer
    {
        protected override void Write(HtmlRenderer renderer, LinkInline link)
        {
            if (!link.IsImage)
            {
                base.Write(renderer, link);
                return;
            }

            if (MediaFiles.ParseReference(link.Url) is not { } id || !media.TryGetValue(id, out var item))
            {
                return;
            }

            var text = string.Concat(link.Descendants<LiteralInline>().Select(literal => literal.Content.ToString())).Trim();
            renderer.Write(item.Kind == MediaKind.Video
                ? MediaMarkup.Video(item, text.Length > 0 ? text : item.AltText)
                : MediaMarkup.Image(item, text.Length > 0 ? text : item.AltText, MediaMarkup.FrameSizes, ImageLoading.Lazy));
        }
    }
}
