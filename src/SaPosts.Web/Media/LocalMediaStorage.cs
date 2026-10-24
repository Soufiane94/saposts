using Microsoft.Extensions.Options;

using SaPosts.Web.Data;

namespace SaPosts.Web.Media;

/// <summary>One folder per media item under <see cref="StorageOptions.MediaPath"/>.</summary>
public sealed class LocalMediaStorage(IOptions<StorageOptions> options) : IMediaStorage
{
    private string Root => options.Value.MediaPath;

    public async Task WriteAsync(Guid mediaId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        var path = PathFor(mediaId, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write then rename, so a reader never sees a half-written file.
        var temporary = path + ".tmp";
        await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        File.Move(temporary, path, overwrite: true);
    }

    public Task ImportAsync(Guid mediaId, string fileName, string localFile, CancellationToken cancellationToken)
    {
        var path = PathFor(mediaId, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Move(localFile, path); // staging is on the same volume: a rename, not a copy
        return Task.CompletedTask;
    }

    public Task<StoredFile?> OpenReadAsync(Guid mediaId, string fileName, CancellationToken cancellationToken)
    {
        var info = new FileInfo(PathFor(mediaId, fileName));
        if (!info.Exists)
        {
            return Task.FromResult<StoredFile?>(null);
        }

        var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<StoredFile?>(new StoredFile(stream, info.Length, info.LastWriteTimeUtc));
    }

    public Task DeleteAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Root, mediaId.ToString("N"));
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        return Task.CompletedTask;
    }

    private string PathFor(Guid mediaId, string fileName)
    {
        if (!MediaFiles.TryGetContentType(fileName, out _))
        {
            throw new ArgumentException($"Invalid media file name '{fileName}'.", nameof(fileName));
        }

        return Path.Combine(Root, mediaId.ToString("N"), fileName);
    }
}
