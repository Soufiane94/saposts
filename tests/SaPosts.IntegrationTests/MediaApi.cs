using System.Net.Http.Headers;
using System.Net.Http.Json;

using SaPosts.Web.Media;

namespace SaPosts.IntegrationTests;

/// <summary>Uploads like media-upload.js does: form fields first, then the file, token in a header.</summary>
internal static class MediaApi
{
    public static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, byte[] content, string fileName, string contentType, string? alt = null,
        Dictionary<string, string>? fields = null, bool withToken = true)
    {
        using var form = new MultipartFormDataContent();
        if (alt is not null)
        {
            form.Add(new StringContent(alt), "alt");
        }

        foreach (var (name, value) in fields ?? [])
        {
            form.Add(new StringContent(value), name);
        }

        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/admin/api/media", UriKind.Relative)) { Content = form };
        if (withToken)
        {
            request.Headers.Add("RequestVerificationToken", await AdminSession.AntiforgeryTokenAsync(client, "/admin/media"));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<MediaEndpoints.UploadResponse> UploadOkAsync(
        HttpClient client, byte[] content, string fileName, string contentType, string? alt = null, Dictionary<string, string>? fields = null)
    {
        var response = await UploadAsync(client, content, fileName, contentType, alt, fields);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"Upload failed with {(int)response.StatusCode}: {body}");
        return (await response.Content.ReadFromJsonAsync<MediaEndpoints.UploadResponse>(TestContext.Current.CancellationToken))!;
    }
}
