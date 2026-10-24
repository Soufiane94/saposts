using Microsoft.Extensions.Options;

using SaPosts.Web.Infrastructure;
using SaPosts.Web.Posts;

namespace SaPosts.UnitTests;

public sealed class SiteTimeTests
{
    private readonly SiteTime siteTime = new(Options.Create(new SiteOptions { TimeZone = "Europe/Paris" }));

    [Theory]
    [InlineData("2026-01-15T10:00", "2026-01-15T09:00:00Z")] // winter: UTC+1
    [InlineData("2026-07-15T10:00", "2026-07-15T08:00:00Z")] // summer: UTC+2
    public void Local_paris_times_convert_to_utc(string local, string expectedUtc)
    {
        var utc = siteTime.ToUtc(DateTime.Parse(local, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(expectedUtc, SiteTime.ToIso(utc));
    }

    [Fact]
    public void A_time_skipped_by_the_spring_clock_change_is_invalid()
    {
        Assert.False(siteTime.IsValidLocal(new DateTime(2026, 3, 29, 2, 30, 0)));
        Assert.True(siteTime.IsValidLocal(new DateTime(2026, 3, 29, 3, 30, 0)));
    }

    [Fact]
    public void Dates_are_formatted_in_the_language_of_the_content()
    {
        var utc = new DateTime(2026, 10, 24, 22, 30, 0, DateTimeKind.Utc); // already the 25th in Paris

        Assert.Equal("25 octobre 2026", siteTime.FormatDate(utc, PostLanguage.French));
        Assert.Equal("25 October 2026", siteTime.FormatDate(utc, PostLanguage.English));
    }
}
