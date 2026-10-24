using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace SaPosts.Web.Media;

public enum ImageLoading
{
    /// <summary>Below the fold: loaded when about to scroll into view.</summary>
    Lazy,

    /// <summary>Likely the largest element on screen (LCP): fetched first.</summary>
    Priority,

    /// <summary>Near the top but not the main element.</summary>
    Eager,
}

/// <summary>
/// HTML for library media, shared by the Markdown renderer (post bodies) and Razor (covers, lists),
/// so every image gets the same responsive markup.
/// </summary>
public static class MediaMarkup
{
    // Slot widths matching the layout in tokens.css: --width-frame is 52rem, gutters 2rem.
    public const string FrameSizes = "(min-width: 56rem) 52rem, calc(100vw - 2rem)";
    public const string ThumbnailSizes = "(min-width: 40rem) 12rem, calc(100vw - 2rem)";
    public const string AdminCardSizes = "12rem";

    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static string Image(MediaItem image, string? alt, string sizes, ImageLoading loading)
    {
        var html = new StringBuilder("<picture>");
        foreach (var extension in MediaFiles.ModernExtensions(image))
        {
            html.Append(CultureInfo.InvariantCulture, $"<source type=\"image/{extension}\" srcset=\"{Srcset(image, extension)}\" sizes=\"{sizes}\">");
        }

        var fallback = MediaFiles.FallbackExtension(image);
        var srcWidth = image.Widths.LastOrDefault(w => w <= 1200, image.Widths[0]);
        html.Append(CultureInfo.InvariantCulture, $"<img src=\"{MediaFiles.Url(image.Id, MediaFiles.Variant(srcWidth, fallback))}\"");
        html.Append(CultureInfo.InvariantCulture, $" srcset=\"{Srcset(image, fallback)}\" sizes=\"{sizes}\"");
        html.Append(CultureInfo.InvariantCulture, $" width=\"{image.Width}\" height=\"{image.Height}\" alt=\"{Encoder.Encode(alt ?? string.Empty)}\"");
        html.Append(loading switch
        {
            ImageLoading.Priority => " fetchpriority=\"high\"",
            ImageLoading.Eager => string.Empty,
            _ => " loading=\"lazy\" decoding=\"async\"",
        });
        return html.Append("></picture>").ToString();
    }

    public static string Video(MediaItem video, string? label)
    {
        var html = new StringBuilder("<video controls preload=\"metadata\" playsinline");
        if (video.Width is { } width && video.Height is { } height)
        {
            html.Append(CultureInfo.InvariantCulture, $" width=\"{width}\" height=\"{height}\"");
        }

        if (video.Poster is { Kind: MediaKind.Image } poster)
        {
            var posterWidth = poster.Widths.LastOrDefault(w => w <= 1200, poster.Widths[0]);
            html.Append(CultureInfo.InvariantCulture, $" poster=\"{MediaFiles.Url(poster.Id, MediaFiles.Variant(posterWidth, "webp"))}\"");
        }

        if (!string.IsNullOrWhiteSpace(label))
        {
            html.Append(CultureInfo.InvariantCulture, $" aria-label=\"{Encoder.Encode(label)}\"");
        }

        var url = MediaFiles.Url(video.Id, MediaFiles.Original(video));
        html.Append(CultureInfo.InvariantCulture, $"><source src=\"{url}\" type=\"{video.ContentType}\">");
        html.Append(CultureInfo.InvariantCulture, $"<a href=\"{url}\">Télécharger la vidéo</a>");
        return html.Append("</video>").ToString();
    }

    /// <summary>Smallest WebP variant, for admin previews.</summary>
    public static string ThumbnailUrl(MediaItem image) => MediaFiles.Url(image.Id, MediaFiles.Variant(image.Widths[0], "webp"));

    private static string Srcset(MediaItem image, string extension) =>
        string.Join(", ", image.Widths.Select(w => $"{MediaFiles.Url(image.Id, MediaFiles.Variant(w, extension))} {w}w"));
}
