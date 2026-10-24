using System.Net;
using System.Net.Http.Headers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NetVips;

using SaPosts.Web.Data;
using SaPosts.Web.Media;

namespace SaPosts.IntegrationTests;

public sealed class MediaTests(BlogFactory factory) : IClassFixture<BlogFactory>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_image_is_resized_into_modern_formats_without_any_metadata()
    {
        var upload = TestMedia.JpegWithLocation();
        using (var original = Image.NewFromBuffer(upload))
        {
            Assert.True(original.Contains("exif-data"), "fixture should carry EXIF");
        }

        var admin = await AdminSession.SignedInClientAsync(factory);
        var media = await MediaApi.UploadOkAsync(admin, upload, "vacances.jpg", "image/jpeg", alt: "Le port au matin");

        Assert.Equal("image", media.Kind);
        Assert.Equal($"![Le port au matin](/media/{media.Id})", media.Markdown);
        var item = await FindAsync(media.Id);
        Assert.Equal([400, 800, 1000], item.Widths);
        Assert.Equal((1000, 700), (item.Width, item.Height));

        var files = Directory.GetFiles(Path.Combine(factory.MediaPath, media.Id.ToString("N"))).Select(Path.GetFileName).Order().ToArray();
        Assert.Equal(["1000.avif", "1000.jpg", "1000.webp", "400.avif", "400.jpg", "400.webp", "800.avif", "800.jpg", "800.webp", "og.jpg"], files);

        var visitor = factory.CreateClient();
        foreach (var file in files)
        {
            var bytes = await visitor.GetByteArrayAsync(new Uri($"/media/{media.Id}/{file}", UriKind.Relative), CancellationToken);
            Assert.False(TestMedia.ContainsMetadata(bytes), $"{file} still carries metadata");
            using var decoded = Image.NewFromBuffer(bytes);
            Assert.False(decoded.Contains("exif-data"), $"{file} has an EXIF block");
        }

        using var preview = Image.NewFromBuffer(await visitor.GetByteArrayAsync(new Uri($"/media/{media.Id}/og.jpg", UriKind.Relative), CancellationToken));
        Assert.Equal((1200, 630), (preview.Width, preview.Height));
    }

    [Fact]
    public async Task Media_files_are_served_with_long_immutable_caching()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var media = await MediaApi.UploadOkAsync(admin, TestMedia.Jpeg(500, 300), "a.jpg", "image/jpeg", alt: "Un aplat orange");

        var response = await factory.CreateClient().GetAsync(new Uri($"/media/{media.Id}/500.avif", UriKind.Relative), CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/avif", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
        Assert.NotNull(response.Headers.ETag);
    }

    public static TheoryData<string, string, string[]> ImageFormats => new()
    {
        { "png-alpha", "image/png", ["600.png", "600.avif", "600.webp"] },
        { "webp", "image/webp", ["900.jpg", "900.avif", "900.webp"] },
        { "avif", "image/avif", ["900.jpg", "900.avif", "900.webp"] },
        { "gif", "image/gif", ["300.png", "300.avif", "300.webp"] }, // libvips decodes GIF with an alpha channel
        { "gif-animated", "image/gif", ["120.gif", "120.webp"] },
    };

    [Theory]
    [MemberData(nameof(ImageFormats))]
    public async Task Every_accepted_image_format_is_decoded(string fixture, string contentType, string[] expectedFiles)
    {
        var content = fixture switch
        {
            "png-alpha" => TestMedia.Png(),
            "webp" => TestMedia.Webp(),
            "avif" => TestMedia.Avif(),
            "gif" => TestMedia.Gif(),
            _ => TestMedia.AnimatedGif(),
        };
        var admin = await AdminSession.SignedInClientAsync(factory);

        var media = await MediaApi.UploadOkAsync(admin, content, $"{fixture}.bin", contentType, alt: "Un test");

        var files = Directory.GetFiles(Path.Combine(factory.MediaPath, media.Id.ToString("N"))).Select(Path.GetFileName).ToArray();
        Assert.All(expectedFiles, expected => Assert.Contains(expected, files));
        Assert.Equal(fixture == "gif-animated", (await FindAsync(media.Id)).IsAnimated);
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg", "Bonjour, je suis un fichier texte.")]
    [InlineData("dessin.svg", "image/svg+xml", "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    public async Task Files_are_identified_by_content_not_by_name_or_declared_type(string fileName, string contentType, string content)
    {
        var admin = await AdminSession.SignedInClientAsync(factory);

        var response = await MediaApi.UploadAsync(admin, System.Text.Encoding.UTF8.GetBytes(content), fileName, contentType, alt: "Peu importe");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task A_broken_image_is_rejected()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var truncated = TestMedia.Jpeg()[..400];

        var response = await MediaApi.UploadAsync(admin, truncated, "coupee.jpg", "image/jpeg", alt: "Image coupée");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Images_require_alt_text()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);

        var response = await MediaApi.UploadAsync(admin, TestMedia.Jpeg(300, 200), "sans-alt.jpg", "image/jpeg", alt: "  ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("texte alternatif", await response.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uploads_over_the_limit_are_refused_while_streaming()
    {
        await using var limited = new BlogFactory { Settings = { ["Media:MaxImageBytes"] = "2000", ["Media:MaxVideoBytes"] = "3000" } };
        var admin = await AdminSession.SignedInClientAsync(limited);

        var image = await MediaApi.UploadAsync(admin, TestMedia.Jpeg(), "lourde.jpg", "image/jpeg", alt: "Trop lourde");
        var video = await MediaApi.UploadAsync(admin, TestMedia.Mp4(fastStart: true), "lourde.mp4", "video/mp4");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, image.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, video.StatusCode);
        Assert.Empty(Directory.GetFiles(Path.Combine(limited.MediaPath, ".staging")));
    }

    [Fact]
    public async Task Videos_are_stored_as_uploaded_and_served_with_range_support()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var content = TestMedia.Mp4(fastStart: true);

        var media = await MediaApi.UploadOkAsync(admin, content, "demo.mp4", "video/mp4", fields: new() { ["width"] = "1280", ["height"] = "720" });

        Assert.Equal("video", media.Kind);
        Assert.Null(media.Warning);
        var item = await FindAsync(media.Id);
        Assert.Equal((1280, 720), (item.Width, item.Height));

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/media/{media.Id}/original.mp4", UriKind.Relative));
        request.Headers.Range = new RangeHeaderValue(0, 15);
        var response = await factory.CreateClient().SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal($"bytes 0-15/{content.Length}", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(content[..16], await response.Content.ReadAsByteArrayAsync(CancellationToken));
    }

    [Fact]
    public async Task A_video_without_faststart_is_accepted_with_a_warning()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);

        var media = await MediaApi.UploadOkAsync(admin, TestMedia.Mp4(fastStart: false), "lente.mp4", "video/mp4");

        Assert.Contains("faststart", media.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webm_is_accepted_but_quicktime_is_not()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);

        var webm = await MediaApi.UploadAsync(admin, TestMedia.Webm(), "clip.webm", "video/webm");
        var mov = await MediaApi.UploadAsync(admin, TestMedia.Mp4(fastStart: true, majorBrand: "qt  "), "clip.mov", "video/quicktime");

        Assert.Equal(HttpStatusCode.OK, webm.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, mov.StatusCode);
    }

    [Fact]
    public async Task Uploading_requires_a_signed_in_admin_and_an_antiforgery_token()
    {
        var anonymous = await MediaApi.UploadAsync(factory.CreateClientWithoutRedirects(), TestMedia.Jpeg(100, 100), "a.jpg", "image/jpeg", alt: "a", withToken: false);
        var admin = await AdminSession.SignedInClientAsync(factory);
        var withoutToken = await MediaApi.UploadAsync(admin, TestMedia.Jpeg(100, 100), "a.jpg", "image/jpeg", alt: "a", withToken: false);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
    }

    [Fact]
    public void Libvips_refuses_to_load_formats_other_than_the_accepted_ones()
    {
        _ = factory.Services.GetRequiredService<ImageProcessor>(); // its static constructor sets the loader blocklist
        byte[] tiff;
        using (var image = Image.Black(8, 8))
        {
            tiff = image.WriteToBuffer(".tif"); // savers are not blocked
        }

        Assert.Throws<VipsException>(() => Image.NewFromBuffer("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"8\"/>"u8.ToArray()));
        Assert.Throws<VipsException>(() => Image.NewFromBuffer(tiff));
        using var jpeg = Image.NewFromBuffer(TestMedia.Jpeg(8, 8)); // still allowed
        Assert.Equal(8, jpeg.Width);
    }

    [Fact]
    public async Task Requests_for_files_outside_a_media_folder_are_not_served()
    {
        var visitor = factory.CreateClient();

        foreach (var url in new[] { $"/media/{Guid.NewGuid()}/..%2F..%2Fsaposts.db", $"/media/{Guid.NewGuid()}/original.exe", $"/media/{Guid.NewGuid()}/400.webp" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri(url, UriKind.Relative), CancellationToken)).StatusCode);
        }
    }

    private async Task<MediaItem> FindAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<BlogDbContext>().Media.AsNoTracking().SingleAsync(m => m.Id == id, CancellationToken);
    }
}
