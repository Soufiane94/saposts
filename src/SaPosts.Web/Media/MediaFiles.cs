using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SaPosts.Web.Media;

/// <summary>Names and URLs of the files that belong to a media item.</summary>
public static partial class MediaFiles
{
    public const string OpenGraph = "og.jpg";

    public static string Variant(int width, string extension) => $"{width}.{extension}";

    public static string Original(MediaItem video) => video.ContentType == "video/webm" ? "original.webm" : "original.mp4";

    public static string Url(Guid id, string fileName) => $"/media/{id:D}/{fileName}";

    /// <summary>How posts reference a library item in Markdown: <c>![alt](/media/{id})</c>.</summary>
    public static string Reference(Guid id) => $"/media/{id:D}";

    public static Guid? ParseReference(string? url) =>
        url is not null && ReferenceRegex().Match(url) is { Success: true } match && Guid.TryParse(match.Groups["id"].ValueSpan, out var id)
            ? id
            : null;

    /// <summary>Image formats offered in &lt;source&gt; elements, best first.</summary>
    public static IReadOnlyList<string> ModernExtensions(MediaItem image) => image.IsAnimated ? ["webp"] : ["avif", "webp"];

    /// <summary>The format every browser and feed reader can display.</summary>
    public static string FallbackExtension(MediaItem image) => image.IsAnimated ? "gif" : image.HasAlpha ? "png" : "jpg";

    public static bool TryGetContentType(string fileName, [NotNullWhen(true)] out string? contentType)
    {
        contentType = FileNameRegex().Match(fileName) is { Success: true } match
            ? match.Groups["ext"].Value switch
            {
                "avif" => "image/avif",
                "webp" => "image/webp",
                "jpg" => "image/jpeg",
                "png" => "image/png",
                "gif" => "image/gif",
                "mp4" => "video/mp4",
                "webm" => "video/webm",
                _ => null,
            }
            : null;
        return contentType is not null;
    }

    // Also the guard against path traversal in storage: nothing but "name.ext".
    [GeneratedRegex("^[a-z0-9]+\\.(?<ext>avif|webp|jpg|png|gif|mp4|webm)$")]
    private static partial Regex FileNameRegex();

    [GeneratedRegex("^/media/(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/?$")]
    private static partial Regex ReferenceRegex();
}
