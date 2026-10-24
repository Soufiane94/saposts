namespace SaPosts.Web.Posts;

/// <summary>A former slug of a post that was published: requests for it get a 301 to the current address.</summary>
public sealed class PostSlugRedirect
{
    public int Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public int PostId { get; set; }

    public Post Post { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
