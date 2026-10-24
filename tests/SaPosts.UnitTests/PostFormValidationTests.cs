using System.ComponentModel.DataAnnotations;

using SaPosts.Web.Pages.Admin.Posts;

namespace SaPosts.UnitTests;

public sealed class PostFormValidationTests
{
    private static PostForm ValidForm() => new() { Title = "Un titre", Excerpt = "Un extrait.", Slug = "un-titre" };

    private static List<ValidationResult> Validate(PostForm form)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void A_complete_form_is_valid() => Assert.Empty(Validate(ValidForm()));

    [Fact]
    public void Slug_may_be_left_empty_to_be_generated()
    {
        var form = ValidForm();
        form.Slug = null;

        Assert.Empty(Validate(form));
    }

    [Theory]
    [InlineData("Avec Majuscule")]
    [InlineData("accentué")]
    [InlineData("double--tiret")]
    public void An_invalid_slug_is_rejected_with_a_french_message(string slug)
    {
        var form = ValidForm();
        form.Slug = slug;

        var error = Assert.Single(Validate(form));
        Assert.Equal([nameof(PostForm.Slug)], error.MemberNames);
        Assert.StartsWith("Le slug ne peut contenir", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Title_and_excerpt_are_required()
    {
        var errors = Validate(new PostForm { Title = null!, Excerpt = null! });

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PostForm.Title)) && e.ErrorMessage == "Saisissez un titre.");
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PostForm.Excerpt)));
    }

    [Fact]
    public void Title_is_limited_to_200_characters()
    {
        var form = ValidForm();
        form.Title = new string('a', 201);

        var error = Assert.Single(Validate(form));
        Assert.Equal([nameof(PostForm.Title)], error.MemberNames);
    }
}
