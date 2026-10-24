using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Data;
using SaPosts.Web.Infrastructure;
using SaPosts.Web.Media;
using SaPosts.Web.Posts;

namespace SaPosts.Web.Pages.Admin.Posts;

public sealed record CoverOption(Guid Id, string Label, string ThumbnailUrl);

public sealed class EditModel(BlogDbContext db, PostEditor editor, MarkdownRenderer renderer, SiteTime siteTime) : PageModel
{
    [BindProperty]
    public PostForm Form { get; set; } = new();

    public int? PostId { get; private set; }

    public string? PublicUrl { get; private set; }

    public bool Saved { get; private set; }

    public string PreviewHtml { get; private set; } = string.Empty;

    public IReadOnlyList<CoverOption> CoverOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? id, bool saved, CancellationToken cancellationToken)
    {
        if (id is not null)
        {
            var post = await db.Posts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (post is null)
            {
                return NotFound();
            }

            PostId = post.Id;
            Saved = saved;
            Form = PostForm.From(post, siteTime.ToLocal);
            PreviewHtml = post.BodyHtml;
            PublicUrl = post.Status == PostStatus.Published ? Url.Page("/Post", new { slug = post.Slug }) : null;
        }

        await LoadCoverOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken cancellationToken)
    {
        PostId = id;

        DateTime? publishedAtUtc = null;
        if (Form.PublishedAt is { } local)
        {
            if (siteTime.IsValidLocal(local))
            {
                publishedAtUtc = siteTime.ToUtc(local);
            }
            else
            {
                ModelState.AddModelError($"{nameof(Form)}.{nameof(PostForm.PublishedAt)}", "Cette heure n’existe pas ce jour-là (passage à l’heure d’été).");
            }
        }

        if (ModelState.IsValid)
        {
            var result = await editor.SaveAsync(id, Form.ToChanges(publishedAtUtc), cancellationToken);
            switch (result.Outcome)
            {
                case PostSaveOutcome.Saved:
                    return RedirectToPage(new { id = result.PostId, saved = true });
                case PostSaveOutcome.NotFound:
                    return NotFound();
                case PostSaveOutcome.SlugTaken:
                    ModelState.AddModelError($"{nameof(Form)}.{nameof(PostForm.Slug)}", "Ce slug est déjà utilisé par un autre article.");
                    break;
                case PostSaveOutcome.InvalidCover:
                    ModelState.AddModelError($"{nameof(Form)}.{nameof(PostForm.CoverMediaId)}", "Choisissez une image de la médiathèque.");
                    break;
            }
        }

        PreviewHtml = await renderer.RenderAsync(Form.BodyMarkdown, cancellationToken);
        await LoadCoverOptionsAsync(cancellationToken);
        return Page();
    }

    /// <summary>Live preview for the editor: the exact HTML the post would get when saved.</summary>
    public async Task<ContentResult> OnPostPreviewAsync([FromForm] string? markdown, CancellationToken cancellationToken) =>
        Content(await renderer.RenderAsync(markdown, cancellationToken), "text/html; charset=utf-8");

    private async Task LoadCoverOptionsAsync(CancellationToken cancellationToken)
    {
        var images = await db.Media.AsNoTracking()
            .Where(m => m.Kind == MediaKind.Image)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync(cancellationToken);
        CoverOptions = images
            .Select(m => new CoverOption(m.Id, $"{m.AltText} ({siteTime.FormatDateTime(m.UploadedAt)})", MediaMarkup.ThumbnailUrl(m)))
            .ToList();
    }
}
