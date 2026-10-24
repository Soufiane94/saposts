using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Data;
using SaPosts.Web.Posts;

namespace SaPosts.Web.Pages;

public sealed class PostModel(BlogDbContext db) : PageModel
{
    public Post Post { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var post = await db.Posts.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Slug == slug && p.Status == PostStatus.Published, cancellationToken);
        if (post is not null)
        {
            Post = post;
            return Page();
        }

        var currentSlug = await db.PostSlugRedirects.AsNoTracking()
            .Where(r => r.Slug == slug && r.Post.Status == PostStatus.Published)
            .Select(r => r.Post.Slug)
            .SingleOrDefaultAsync(cancellationToken);

        return currentSlug is null ? NotFound() : RedirectToPagePermanent("/Post", new { slug = currentSlug });
    }
}
