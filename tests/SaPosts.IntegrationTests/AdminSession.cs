using System.Text.RegularExpressions;

namespace SaPosts.IntegrationTests;

/// <summary>Drives the admin forms like a browser: fetch the page, then post with its antiforgery token.</summary>
internal static partial class AdminSession
{
    public static Task<HttpClient> SignedInClientAsync(BlogFactory factory) => factory.AdminClientAsync();

    public static async Task<HttpClient> LogInNewClientAsync(BlogFactory factory)
    {
        var client = factory.CreateClientWithoutRedirects();
        var response = await LogInAsync(client, BlogFactory.AdminEmail, BlogFactory.AdminPassword);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    public static Task<HttpResponseMessage> LogInAsync(HttpClient client, string email, string password, string? returnUrl = null)
    {
        var url = returnUrl is null ? "/admin/login" : $"/admin/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        return SubmitFormAsync(client, url, url, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
        });
    }

    public static Task<HttpResponseMessage> LogOutAsync(HttpClient client) =>
        SubmitFormAsync(client, "/admin/posts", "/admin/logout", new Dictionary<string, string>());

    public static async Task<string> AntiforgeryTokenAsync(HttpClient client, string page)
    {
        var html = await client.GetStringAsync(new Uri(page, UriKind.Relative), TestContext.Current.CancellationToken);
        return AntiforgeryToken().Match(html).Groups["token"].Value;
    }

    /// <summary>Gets <paramref name="formPage"/> for its antiforgery token, then posts <paramref name="fields"/> to <paramref name="action"/>.</summary>
    public static async Task<HttpResponseMessage> SubmitFormAsync(
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
