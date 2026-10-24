using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Data;
using SaPosts.Web.Media;

namespace SaPosts.Web.Pages.Admin.Media;

public sealed class IndexModel(BlogDbContext db) : PageModel
{
    public IReadOnlyList<MediaItem> Items { get; private set; } = [];

    public bool Deleted { get; private set; }

    public async Task OnGetAsync(bool deleted, CancellationToken cancellationToken)
    {
        Deleted = deleted;
        Items = await db.Media.AsNoTracking()
            .Include(m => m.Poster)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync(cancellationToken);
    }
}
