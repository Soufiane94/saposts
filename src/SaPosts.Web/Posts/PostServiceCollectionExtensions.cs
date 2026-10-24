namespace SaPosts.Web.Posts;

public static class PostServiceCollectionExtensions
{
    public static IServiceCollection AddPosts(this IServiceCollection services)
    {
        // Scoped: an HtmlSanitizer instance is not documented as safe for concurrent use.
        services.AddScoped<MarkdownRenderer>();
        services.AddScoped<PostEditor>();
        return services;
    }
}
