using SaPosts.Web.Media;

namespace SaPosts.Web.Posts;

public sealed class Post
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    /// <summary>Language of the content; rendered as the article's <c>lang</c> attribute.</summary>
    public PostLanguage Language { get; set; } = PostLanguage.French;

    public string BodyMarkdown { get; set; } = string.Empty;

    /// <summary>Sanitized HTML rendered from <see cref="BodyMarkdown"/> when the post is saved.</summary>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>Version of the Markdown pipeline that produced <see cref="BodyHtml"/>.</summary>
    public int RendererVersion { get; set; }

    public PostStatus Status { get; set; } = PostStatus.Draft;

    /// <summary>UTC. Set on first publish, editable afterwards.</summary>
    public DateTime? PublishedAt { get; set; }

    public Guid? CoverMediaId { get; set; }

    public MediaItem? CoverMedia { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
