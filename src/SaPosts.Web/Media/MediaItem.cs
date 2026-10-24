namespace SaPosts.Web.Media;

/// <summary>An uploaded image or video. Its files live in media storage, keyed by <see cref="Id"/>.</summary>
public sealed class MediaItem
{
    public Guid Id { get; set; }

    public MediaKind Kind { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Detected from the file content, not from the name or the upload headers.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    /// <summary>Required for images (enforced on upload).</summary>
    public string? AltText { get; set; }

    /// <summary>Optional image shown before a video plays.</summary>
    public Guid? PosterId { get; set; }

    public MediaItem? Poster { get; set; }

    public DateTime UploadedAt { get; set; }
}
