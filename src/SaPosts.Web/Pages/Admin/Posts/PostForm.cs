using System.ComponentModel.DataAnnotations;

using SaPosts.Web.Posts;

namespace SaPosts.Web.Pages.Admin.Posts;

/// <summary>The post editor form. <see cref="PublishedAt"/> is in the site's time zone, as typed.</summary>
public sealed class PostForm
{
    [Display(Name = "Titre")]
    [Required(ErrorMessage = "Saisissez un titre.")]
    [StringLength(200, ErrorMessage = "Le titre ne doit pas dépasser 200 caractères.")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Slug (adresse de l’article)")]
    [StringLength(200, ErrorMessage = "Le slug ne doit pas dépasser 200 caractères.")]
    [RegularExpression(Slugs.Pattern, ErrorMessage = "Le slug ne peut contenir que des lettres minuscules sans accents, des chiffres et des tirets simples.")]
    public string? Slug { get; set; }

    [Display(Name = "Extrait")]
    [Required(ErrorMessage = "Saisissez un extrait : il apparaît sur la page d’accueil et dans les aperçus de liens.")]
    [StringLength(500, ErrorMessage = "L’extrait ne doit pas dépasser 500 caractères.")]
    public string Excerpt { get; set; } = string.Empty;

    [Display(Name = "Langue")]
    public PostLanguage Language { get; set; } = PostLanguage.French;

    [Display(Name = "Statut")]
    public PostStatus Status { get; set; } = PostStatus.Draft;

    [Display(Name = "Date de publication")]
    public DateTime? PublishedAt { get; set; }

    [Display(Name = "Contenu (Markdown)")]
    [StringLength(200_000, ErrorMessage = "Le contenu ne doit pas dépasser 200 000 caractères.")]
    public string? BodyMarkdown { get; set; }

    public static PostForm From(Post post, Func<DateTime, DateTime> toLocal) => new()
    {
        Title = post.Title,
        Slug = post.Slug,
        Excerpt = post.Excerpt,
        Language = post.Language,
        Status = post.Status,
        PublishedAt = post.PublishedAt is { } publishedAt ? toLocal(publishedAt) : null,
        BodyMarkdown = post.BodyMarkdown,
    };

    public PostChanges ToChanges(DateTime? publishedAtUtc) =>
        new(Title, Slug, Excerpt, Language, Status, publishedAtUtc, BodyMarkdown ?? string.Empty);
}
