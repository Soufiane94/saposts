using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SaPosts.Web.Posts;

/// <summary>URL slugs: lowercase ASCII letters and digits, separated by single hyphens.</summary>
public static partial class Slugs
{
    public const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";

    /// <summary>Generated slugs are cut (at a hyphen) to stay readable in shared links.</summary>
    public const int GeneratedMaxLength = 80;

    private const string Fallback = "article";

    public static string FromTitle(string title)
    {
        var slug = new StringBuilder(title.Length);
        var pendingHyphen = false;

        // FormD splits "é" into "e" + a combining accent, which is then dropped.
        foreach (var character in title.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            var ascii = lower switch
            {
                'œ' => "oe",
                'æ' => "ae",
                'ß' => "ss",
                'ø' => "o",
                'ł' => "l",
                'đ' => "d",
                >= 'a' and <= 'z' or >= '0' and <= '9' => lower.ToString(),
                _ => null,
            };

            if (ascii is null)
            {
                pendingHyphen = slug.Length > 0;
                continue;
            }

            if (pendingHyphen)
            {
                slug.Append('-');
                pendingHyphen = false;
            }

            slug.Append(ascii);
        }

        if (slug.Length > GeneratedMaxLength)
        {
            var cut = slug.ToString(0, GeneratedMaxLength + 1).LastIndexOf('-');
            slug.Length = cut > 0 ? cut : GeneratedMaxLength;
        }

        return slug.Length == 0 ? Fallback : slug.ToString();
    }

    public static bool IsValid(string slug) => SlugRegex().IsMatch(slug);

    [GeneratedRegex(Pattern)]
    private static partial Regex SlugRegex();
}
