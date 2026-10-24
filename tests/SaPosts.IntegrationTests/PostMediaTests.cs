using System.Net;

namespace SaPosts.IntegrationTests;

public sealed class PostMediaTests(BlogFactory factory) : IClassFixture<BlogFactory>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_post_shows_its_cover_and_embedded_media()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var cover = await MediaApi.UploadOkAsync(admin, TestMedia.Jpeg(1600, 900), "couverture.jpg", "image/jpeg", alt: "Vue sur la baie");
        var photo = await MediaApi.UploadOkAsync(admin, TestMedia.Jpeg(800, 600), "photo.jpg", "image/jpeg", alt: "Un détail");
        var clip = await MediaApi.UploadOkAsync(admin, TestMedia.Mp4(fastStart: true), "clip.mp4", "video/mp4", fields: new() { ["width"] = "640", ["height"] = "360" });

        var saved = await SavePostAsync(admin, "Avec des médias", $"Intro.\n\n{photo.Markdown}\n\n{clip.Markdown}", cover.Id);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var visitor = factory.CreateClient();
        var page = await visitor.GetStringAsync(new Uri("/articles/avec-des-medias", UriKind.Relative), CancellationToken);
        Assert.Contains($"<figure class=\"post__cover\"><picture><source type=\"image/avif\" srcset=\"/media/{cover.Id}/400.avif 400w", page, StringComparison.Ordinal);
        Assert.Contains("alt=\"Vue sur la baie\" fetchpriority=\"high\"", page, StringComparison.Ordinal);
        Assert.Contains("alt=\"Un détail\" loading=\"lazy\"", page, StringComparison.Ordinal);
        Assert.Contains($"<source src=\"/media/{clip.Id}/original.mp4\" type=\"video/mp4\">", page, StringComparison.Ordinal);
        Assert.Contains("width=\"640\" height=\"360\"", page, StringComparison.Ordinal);

        var home = await visitor.GetStringAsync(new Uri("/", UriKind.Relative), CancellationToken);
        Assert.Contains("post-summary--with-cover", home, StringComparison.Ordinal);
        Assert.Contains($"/media/{cover.Id}/400.webp 400w", home, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_video_cannot_be_a_cover()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var clip = await MediaApi.UploadOkAsync(admin, TestMedia.Webm(), "clip.webm", "video/webm");

        var response = await SavePostAsync(admin, "Couverture invalide", "Texte.", clip.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Choisissez une image de la médiathèque.", await response.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Media_in_use_cannot_be_deleted_but_unused_media_can()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var used = await MediaApi.UploadOkAsync(admin, TestMedia.Jpeg(400, 300), "utilisee.jpg", "image/jpeg", alt: "Utilisée");
        var unused = await MediaApi.UploadOkAsync(admin, TestMedia.Jpeg(400, 300), "orpheline.jpg", "image/jpeg", alt: "Orpheline");
        await SavePostAsync(admin, "Article qui utilise une image", used.Markdown);

        var refused = await AdminSession.SubmitFormAsync(admin, $"/admin/media/edit/{used.Id}", $"/admin/media/edit/{used.Id}?handler=Delete", []);
        var deleted = await AdminSession.SubmitFormAsync(admin, $"/admin/media/edit/{unused.Id}", $"/admin/media/edit/{unused.Id}?handler=Delete", []);

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("Article qui utilise une image", await refused.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(factory.MediaPath, used.Id.ToString("N"))));

        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(factory.MediaPath, unused.Id.ToString("N"))));
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync(new Uri($"/media/{unused.Id}/400.webp", UriKind.Relative), CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Changing_alt_text_updates_posts_that_embed_the_image()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var image = await MediaApi.UploadOkAsync(admin, TestMedia.Jpeg(400, 300), "maj.jpg", "image/jpeg", alt: "Ancien texte");
        await SavePostAsync(admin, "Alt mis à jour", $"![](/media/{image.Id})");

        var edited = await AdminSession.SubmitFormAsync(admin, $"/admin/media/edit/{image.Id}", $"/admin/media/edit/{image.Id}", new() { ["Form.AltText"] = "Nouveau texte" });

        Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        var page = await factory.CreateClient().GetStringAsync(new Uri("/articles/alt-mis-a-jour", UriKind.Relative), CancellationToken);
        Assert.Contains("alt=\"Nouveau texte\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_image_keeps_a_non_empty_alt_text()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var image = await MediaApi.UploadOkAsync(admin, TestMedia.Jpeg(400, 300), "alt.jpg", "image/jpeg", alt: "Description");

        var response = await AdminSession.SubmitFormAsync(admin, $"/admin/media/edit/{image.Id}", $"/admin/media/edit/{image.Id}", new() { ["Form.AltText"] = "" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Le texte alternatif est obligatoire pour une image.", await response.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> SavePostAsync(HttpClient admin, string title, string body, Guid? coverId = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["Form.Title"] = title,
            ["Form.Excerpt"] = "Résumé.",
            ["Form.Language"] = "French",
            ["Form.Status"] = "Published",
            ["Form.BodyMarkdown"] = body,
        };
        if (coverId is { } id)
        {
            fields["Form.CoverMediaId"] = id.ToString();
        }

        return AdminSession.SubmitFormAsync(admin, "/admin/posts/edit", "/admin/posts/edit", fields);
    }
}
