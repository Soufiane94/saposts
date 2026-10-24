using System.Text.RegularExpressions;

namespace SaPosts.IntegrationTests;

/// <summary>Drives the admin forms like a browser: fetch the page, then post with its antiforgery token.</summary>
internal static partial class AdminSession
{
    public static Task<HttpResponseMessage> LogInAsync(HttpClient client, string email, string password, string? returnUrl = null)
    {
        var url = returnUrl is null ? "/admin/login" : $"/admin/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        return PostFormAsync(client, url, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
        });
    }

    public static Task<HttpResponseMessage> LogOutAsync(HttpClient client) =>
        PostFormAsync(client, "/admin", "/admin/logout", new Dictionary<string, string>());

    private static Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, Dictionary<string, string> fields) =>
        PostFormAsync(client, url, url, fields);

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string formPage, string action, Dictionary<string, string> fields)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var html = await client.GetStringAsync(new Uri(formPage, UriKind.Relative), cancellationToken);
        fields["__RequestVerificationToken"] = AntiforgeryToken().Match(html).Groups["token"].Value;

        using var content = new FormUrlEncodedContent(fields);
        return await client.PostAsync(new Uri(action, UriKind.Relative), content, cancellationToken);
    }

    [GeneratedRegex("""name="__RequestVerificationToken" type="hidden" value="(?<token>[^"]+)""")]
    private static partial Regex AntiforgeryToken();
}
