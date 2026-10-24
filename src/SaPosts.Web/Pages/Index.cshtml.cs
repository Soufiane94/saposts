using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Data;
using SaPosts.Web.Posts;

namespace SaPosts.Web.Pages;

public sealed record PostSummary(string Slug, string Title, string Excerpt, PostLanguage Language, DateTime PublishedAt);

public sealed class IndexModel(BlogDbContext db) : PageModel
{
    public const int PageSize = 10;

    public IReadOnlyList<PostSummary> Posts { get; private set; } = [];

    public int PageNumber { get; private set; } = 1;

    public bool HasOlder { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? pageNumber, CancellationToken cancellationToken)
    {
        switch (pageNumber)
        {
            case < 1:
                return NotFound();
            case 1:
                return RedirectPermanent(PageUrl(1));
        }

        PageNumber = pageNumber ?? 1;

        // One extra row tells whether an older page exists, without a COUNT query.
        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.Status == PostStatus.Published)
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize + 1)
            .Select(p => new PostSummary(p.Slug, p.Title, p.Excerpt, p.Language, p.PublishedAt!.Value))
            .ToListAsync(cancellationToken);

        if (PageNumber > 1 && posts.Count == 0)
        {
            return NotFound();
        }

        HasOlder = posts.Count > PageSize;
        Posts = posts.Take(PageSize).ToList();
        return Page();
    }

    // Built explicitly: link generation would otherwise reuse the current pageNumber as an ambient value.
    public string PageUrl(int pageNumber) =>
        pageNumber <= 1 ? Url.Content("~/") : Url.Page("/Index", new { pageNumber })!;
}
