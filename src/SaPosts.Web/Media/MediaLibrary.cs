using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Data;
using SaPosts.Web.Posts;

namespace SaPosts.Web.Media;

public enum MediaUpdateOutcome
{
    Saved,
    NotFound,
    AltTextRequired,
    InvalidPoster,
}

/// <summary>Library maintenance: edits that change rendered posts, and deletion only when unused.</summary>
public sealed class MediaLibrary(BlogDbContext db, IMediaStorage storage, PostEditor posts)
{
    public async Task<MediaUpdateOutcome> UpdateAsync(Guid id, string? altText, Guid? posterId, CancellationToken cancellationToken)
    {
        var item = await db.Media.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (item is null)
        {
            return MediaUpdateOutcome.NotFound;
        }

        altText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        if (item.Kind == MediaKind.Image && altText is null)
        {
            return MediaUpdateOutcome.AltTextRequired;
        }

        if (item.Kind == MediaKind.Video && posterId is not null
            && !await db.Media.AnyAsync(m => m.Id == posterId && m.Kind == MediaKind.Image, cancellationToken))
        {
            return MediaUpdateOutcome.InvalidPoster;
        }

        item.AltText = altText;
        item.PosterId = item.Kind == MediaKind.Video ? posterId : null;
        await db.SaveChangesAsync(cancellationToken);

        // Post bodies embed the alt text and poster in their stored HTML.
        await posts.RerenderReferencingAsync(id, cancellationToken);
        return MediaUpdateOutcome.Saved;
    }

    /// <summary>Titles of the posts (and videos, as poster) that use this item; empty when it can be deleted.</summary>
    public async Task<IReadOnlyList<string>> FindUsesAsync(Guid id, CancellationToken cancellationToken)
    {
        var reference = MediaFiles.Reference(id);
        var postTitles = await db.Posts.AsNoTracking()
            .Where(p => p.CoverMediaId == id || p.BodyMarkdown.Contains(reference))
            .OrderBy(p => p.Title)
            .Select(p => p.Title)
            .ToListAsync(cancellationToken);
        var videos = await db.Media.AsNoTracking()
            .Where(m => m.PosterId == id)
            .Select(m => "Vidéo " + m.OriginalFileName)
            .ToListAsync(cancellationToken);
        return [.. postTitles, .. videos];
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if ((await FindUsesAsync(id, cancellationToken)).Count > 0)
        {
            return false;
        }

        var deleted = await db.Media.Where(m => m.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
        if (deleted)
        {
            await storage.DeleteAsync(id, cancellationToken);
        }

        return deleted;
    }
}
