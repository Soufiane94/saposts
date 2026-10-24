using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Data;
using SaPosts.Web.Posts;

namespace SaPosts.Web.Pages.Admin.Posts;

public sealed record PostRow(int Id, string Title, string Slug, PostStatus Status, DateTime? PublishedAt, DateTime UpdatedAt);

public sealed class IndexModel(BlogDbContext db, PostEditor editor) : PageModel
{
    public IReadOnlyList<PostRow> Posts { get; private set; } = [];

    public bool Deleted { get; private set; }

    public async Task OnGetAsync(bool deleted, CancellationToken cancellationToken)
    {
        Deleted = deleted;
        Posts = await db.Posts.AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new PostRow(p.Id, p.Title, p.Slug, p.Status, p.PublishedAt, p.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken) =>
        await editor.DeleteAsync(id, cancellationToken) ? RedirectToPage(new { deleted = true }) : NotFound();
}
