using System.ComponentModel.DataAnnotations;

namespace SaPosts.Web.Media;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    [Range(1, long.MaxValue)]
    public long MaxImageBytes { get; set; } = 25L * 1024 * 1024;

    /// <summary>Short clips only: videos are stored as uploaded, without transcoding.</summary>
    [Range(1, long.MaxValue)]
    public long MaxVideoBytes { get; set; } = 200L * 1024 * 1024;
}
