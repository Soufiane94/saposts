namespace SaPosts.Web.Media;

/// <summary>
/// Where media files live. Local disk today (a Docker volume); this is the only seam to change
/// for object storage later. File names are validated by <see cref="MediaFiles"/>.
/// </summary>
public interface IMediaStorage
{
    Task WriteAsync(Guid mediaId, string fileName, Stream content, CancellationToken cancellationToken);

    /// <summary>Takes ownership of a local file (a staged upload), moving it into storage.</summary>
    Task ImportAsync(Guid mediaId, string fileName, string localFile, CancellationToken cancellationToken);

    /// <summary>A seekable stream (for range requests), or null if the file does not exist.</summary>
    Task<StoredFile?> OpenReadAsync(Guid mediaId, string fileName, CancellationToken cancellationToken);

    Task DeleteAsync(Guid mediaId, CancellationToken cancellationToken);
}

public sealed record StoredFile(Stream Content, long Length, DateTimeOffset LastModified);
