using Microsoft.Extensions.Options;

using SaPosts.Web.Data;

namespace SaPosts.Web.Media;

/// <summary>
/// Temporary files for uploads in progress. They sit on the media volume so that importing a video
/// into local storage is a rename. Leftovers from crashed uploads are removed at startup.
/// </summary>
public sealed class MediaStaging(IOptions<StorageOptions> options)
{
    private string Root => Path.Combine(options.Value.MediaPath, ".staging");

    public string CreatePath()
    {
        Directory.CreateDirectory(Root);
        return Path.Combine(Root, $"{Guid.NewGuid():N}.part");
    }

    public static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Removed at the next startup.
        }
    }

    public void DeleteOlderThan(DateTime utcLimit)
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        foreach (var file in new DirectoryInfo(Root).EnumerateFiles("*.part").Where(f => f.LastWriteTimeUtc < utcLimit))
        {
            Delete(file.FullName);
        }
    }
}
