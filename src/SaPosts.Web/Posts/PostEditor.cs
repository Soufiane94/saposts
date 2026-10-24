using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Data;
using SaPosts.Web.Media;

namespace SaPosts.Web.Posts;

/// <summary>What the admin submits for a post. Dates are UTC.</summary>
public sealed record PostChanges(
    string Title,
    string? Slug,
    string Excerpt,
    PostLanguage Language,
    PostStatus Status,
    DateTime? PublishedAt,
    Guid? CoverMediaId,
    string BodyMarkdown);

public enum PostSaveOutcome
{
    Saved,
    NotFound,
    SlugTaken,
    InvalidCover,
}

public sealed record PostSaveResult(PostSaveOutcome Outcome, int PostId = 0);

public sealed class PostEditor(BlogDbContext db, MarkdownRenderer renderer, TimeProvider clock)
{
    public async Task<PostSaveResult> SaveAsync(int? id, PostChanges changes, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        Post? post;
        if (id is null)
        {
            post = new Post { CreatedAt = now };
            db.Posts.Add(post);
        }
        else
        {
            post = await db.Posts.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (post is null)
            {
                return new PostSaveResult(PostSaveOutcome.NotFound);
            }
        }

        if (changes.CoverMediaId is { } coverId
            && !await db.Media.AnyAsync(m => m.Id == coverId && m.Kind == MediaKind.Image, cancellationToken))
        {
            return new PostSaveResult(PostSaveOutcome.InvalidCover);
        }

        // An explicit slug must be free; a generated one gets a numeric suffix instead.
        string slug;
        if (string.IsNullOrWhiteSpace(changes.Slug))
        {
            slug = await AvailableSlugAsync(Slugs.FromTitle(changes.Title), post.Id, cancellationToken);
        }
        else
        {
            slug = changes.Slug.Trim();
            if (await db.Posts.AnyAsync(p => p.Slug == slug && p.Id != post.Id, cancellationToken))
            {
                return new PostSaveResult(PostSaveOutcome.SlugTaken);
            }
        }

        if (post.Slug != slug)
        {
            await ChangeSlugAsync(post, slug, now, cancellationToken);
        }

        post.Title = changes.Title.Trim();
        post.Excerpt = changes.Excerpt.Trim();
        post.Language = changes.Language;
        post.Status = changes.Status;
        // Set on first publish, editable afterwards; kept if the post goes back to draft.
        post.PublishedAt = changes.PublishedAt ?? post.PublishedAt ?? (changes.Status == PostStatus.Published ? now : null);
        post.CoverMediaId = changes.CoverMediaId;
        post.BodyMarkdown = changes.BodyMarkdown;
        post.BodyHtml = await renderer.RenderAsync(changes.BodyMarkdown, cancellationToken);
        post.RendererVersion = MarkdownRenderer.Version;
        post.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return new PostSaveResult(PostSaveOutcome.Saved, post.Id);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        await db.Posts.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Re-renders posts whose HTML came from an older version of the Markdown pipeline.</summary>
    public Task RerenderOutdatedAsync(CancellationToken cancellationToken) =>
        RerenderAsync(db.Posts.Where(p => p.RendererVersion < MarkdownRenderer.Version), cancellationToken);

    /// <summary>Re-renders posts that embed a media item, after its alt text or poster changed.</summary>
    public Task RerenderReferencingAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        var reference = MediaFiles.Reference(mediaId);
        return RerenderAsync(db.Posts.Where(p => p.BodyMarkdown.Contains(reference)), cancellationToken);
    }

    private async Task RerenderAsync(IQueryable<Post> posts, CancellationToken cancellationToken)
    {
        foreach (var post in await posts.ToListAsync(cancellationToken))
        {
            post.BodyHtml = await renderer.RenderAsync(post.BodyMarkdown, cancellationToken);
            post.RendererVersion = MarkdownRenderer.Version;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ChangeSlugAsync(Post post, string slug, DateTime now, CancellationToken cancellationToken)
    {
        // The slug now belongs to this post, even if it used to redirect somewhere.
        // Tracked removal (not ExecuteDelete) so it commits with the rest of the save.
        db.PostSlugRedirects.RemoveRange(await db.PostSlugRedirects.Where(r => r.Slug == slug).ToListAsync(cancellationToken));

        // A slug that was public keeps working: links already shared get redirected.
        if (post.PublishedAt is not null && post.Slug.Length > 0)
        {
            db.PostSlugRedirects.Add(new PostSlugRedirect { Slug = post.Slug, Post = post, CreatedAt = now });
        }

        post.Slug = slug;
    }

    private async Task<string> AvailableSlugAsync(string baseSlug, int postId, CancellationToken cancellationToken)
    {
        var candidate = baseSlug;
        for (var suffix = 2; await IsTakenAsync(candidate, postId, cancellationToken); suffix++)
        {
            candidate = $"{baseSlug}-{suffix}";
        }

        return candidate;
    }

    // Generated slugs also avoid former slugs, so old links keep pointing to their post.
    private async Task<bool> IsTakenAsync(string slug, int postId, CancellationToken cancellationToken) =>
        await db.Posts.AnyAsync(p => p.Slug == slug && p.Id != postId, cancellationToken)
        || await db.PostSlugRedirects.AnyAsync(r => r.Slug == slug && r.PostId != postId, cancellationToken);
}
