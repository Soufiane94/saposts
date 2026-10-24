using SaPosts.Web.Posts;

namespace SaPosts.UnitTests;

public sealed class SlugsTests
{
    [Theory]
    [InlineData("Bonjour le monde", "bonjour-le-monde")]
    [InlineData("L’été à Paris : 10 idées !", "l-ete-a-paris-10-idees")]
    [InlineData("Œuvre, cœur & Ærø", "oeuvre-coeur-aero")]
    [InlineData("Straße", "strasse")]
    [InlineData("  --Déjà vu--  ", "deja-vu")]
    [InlineData("C# et .NET 10", "c-et-net-10")]
    [InlineData("ÉCRIRE EN MAJUSCULES", "ecrire-en-majuscules")]
    public void FromTitle_produces_readable_ascii(string title, string expected) =>
        Assert.Equal(expected, Slugs.FromTitle(title));

    [Theory]
    [InlineData("")]
    [InlineData("!!! ???")]
    [InlineData("日本語")]
    public void FromTitle_falls_back_when_nothing_is_left(string title) =>
        Assert.Equal("article", Slugs.FromTitle(title));

    [Fact]
    public void FromTitle_cuts_long_titles_at_a_word_boundary()
    {
        var title = string.Join(' ', Enumerable.Repeat("développement", 12));

        var slug = Slugs.FromTitle(title);

        Assert.True(slug.Length <= Slugs.GeneratedMaxLength);
        Assert.EndsWith("developpement", slug, StringComparison.Ordinal);
        Assert.True(Slugs.IsValid(slug));
    }

    [Theory]
    [InlineData("mon-article", true)]
    [InlineData("article-2", true)]
    [InlineData("a", true)]
    [InlineData("Mon-Article", false)]
    [InlineData("mon--article", false)]
    [InlineData("-mon-article", false)]
    [InlineData("mon-article-", false)]
    [InlineData("été", false)]
    [InlineData("mon article", false)]
    [InlineData("mon/article", false)]
    public void IsValid_accepts_only_lowercase_words_joined_by_single_hyphens(string slug, bool expected) =>
        Assert.Equal(expected, Slugs.IsValid(slug));
}
