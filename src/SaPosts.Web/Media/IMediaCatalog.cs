using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Data;

namespace SaPosts.Web.Media;

/// <summary>Looks up library items referenced by a post body (a seam so the renderer is unit-testable).</summary>
public interface IMediaCatalog
{
    Task<IReadOnlyDictionary<Guid, MediaItem>> FindAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

public sealed class MediaCatalog(BlogDbContext db) : IMediaCatalog
{
    public async Task<IReadOnlyDictionary<Guid, MediaItem>> FindAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? new Dictionary<Guid, MediaItem>()
            : await db.Media.AsNoTracking().Include(m => m.Poster).Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, cancellationToken);
}
