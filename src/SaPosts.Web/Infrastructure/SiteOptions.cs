using System.ComponentModel.DataAnnotations;

namespace SaPosts.Web.Infrastructure;

/// <summary>Site identity shown in the header, footer and page titles.</summary>
public sealed class SiteOptions
{
    public const string SectionName = "Site";

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Tagline { get; set; } = string.Empty;

    [Required, Url]
    public string PortfolioUrl { get; set; } = string.Empty;
}
