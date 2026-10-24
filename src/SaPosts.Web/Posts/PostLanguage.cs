namespace SaPosts.Web.Posts;

public enum PostLanguage
{
    French,
    English,
}

public static class PostLanguageExtensions
{
    /// <summary>BCP 47 tag for the <c>lang</c> attribute.</summary>
    public static string ToLanguageTag(this PostLanguage language) => language switch
    {
        PostLanguage.English => "en",
        _ => "fr",
    };
}
