using AngleSharp.Dom;

using Ganss.Xss;

using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Syntax;

namespace SaPosts.Web.Posts;

/// <summary>
/// Markdown to safe HTML: Markdig with raw HTML disabled, then HtmlSanitizer with an explicit allowlist.
/// The same pipeline serves the editor preview and the stored post body.
/// </summary>
public sealed class MarkdownRenderer
{
    /// <summary>Bump when the output changes: posts rendered by an older version are re-rendered at startup.</summary>
    public const int Version = 1;

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

    public string Render(string? markdown)
    {
        var document = Markdown.Parse(markdown ?? string.Empty, Pipeline);

        // The post title is the page's only h1: sections start at "##", and a "#" heading becomes h2 as well.
        foreach (var heading in document.Descendants<HeadingBlock>().Where(h => h.Level == 1))
        {
            heading.Level = 2;
        }

        return sanitizer.Sanitize(Markdown.ToHtml(document, Pipeline));
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        // Starts from the defaults (which screen URI attributes against AllowedSchemes), then narrows them.
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith([
            "p", "h2", "h3", "h4", "h5", "h6", "a", "strong", "em", "del", "code", "pre", "blockquote",
            "ul", "ol", "li", "hr", "br", "img", "table", "thead", "tbody", "tr", "th", "td", "sup", "div",
        ]);

        // No "style": inline styles would be blocked by the CSP anyway (pipe-table alignment is dropped).
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["href", "src", "alt", "title", "id", "class", "start", "loading", "decoding", "tabindex"]);

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
}
