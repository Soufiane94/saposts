using System.Globalization;

using Microsoft.Extensions.Options;

using SaPosts.Web.Posts;

namespace SaPosts.Web.Infrastructure;

/// <summary>Converts between stored UTC dates and the site's time zone, and formats dates for display.</summary>
public sealed class SiteTime(IOptions<SiteOptions> options)
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, zone);

    /// <summary>False for times skipped when clocks go forward (for example 02:30 on the last Sunday of March).</summary>
    public bool IsValidLocal(DateTime local) => !zone.IsInvalidTime(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));

    /// <summary>For an ambiguous time (clocks going back), the standard-time offset is used.</summary>
    public DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);

    /// <summary>"24 octobre 2026" or "24 October 2026", matching the language of the content around it.</summary>
    public string FormatDate(DateTime utc, PostLanguage language) =>
        ToLocal(utc).ToString("d MMMM yyyy", language == PostLanguage.English ? English : French);

    /// <summary>Admin lists: "24/10/2026 09:30".</summary>
    public string FormatDateTime(DateTime utc) => ToLocal(utc).ToString("dd/MM/yyyy HH:mm", French);

    /// <summary>For <c>&lt;time datetime&gt;</c>.</summary>
    public static string ToIso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
