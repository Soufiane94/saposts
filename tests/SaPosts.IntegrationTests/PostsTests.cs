using System.Net;

using Microsoft.Extensions.DependencyInjection;

using SaPosts.Web.Data;
using SaPosts.Web.Posts;

namespace SaPosts.IntegrationTests;

public sealed class PostsTests(BlogFactory factory) : IClassFixture<BlogFactory>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_draft_is_neither_listed_nor_reachable()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);

        var saved = await SavePostAsync(admin, "Brouillon secret", status: "Draft");
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var visitor = factory.CreateClientWithoutRedirects();
        Assert.DoesNotContain("Brouillon secret", await GetAsync(visitor, "/"), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri("/articles/brouillon-secret", UriKind.Relative), CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task A_published_post_is_listed_and_rendered()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);

        await SavePostAsync(admin, "Hello from Paris", status: "Published", language: "English", body: "## First part\n\nSome **bold** text.");

        var visitor = factory.CreateClientWithoutRedirects();
        var home = await GetAsync(visitor, "/");
        Assert.Contains("href=\"/articles/hello-from-paris\"", home, StringComparison.Ordinal);

        var page = await GetAsync(visitor, "/articles/hello-from-paris");
        Assert.Contains("<article class=\"frame post\" lang=\"en\">", page, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"post__title\">Hello from Paris</h1>", page, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"first-part\">First part</h2>", page, StringComparison.Ordinal);
        Assert.Contains("<strong>bold</strong>", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generated_slugs_get_a_suffix_but_a_taken_explicit_slug_is_rejected()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        await SavePostAsync(admin, "Même titre", status: "Published");

        await SavePostAsync(admin, "Même titre", status: "Published");
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync(new Uri("/articles/meme-titre-2", UriKind.Relative), CancellationToken)).StatusCode);

        var rejected = await SavePostAsync(admin, "Autre titre", slug: "meme-titre");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Ce slug est déjà utilisé", await rejected.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Renaming_a_published_post_redirects_its_old_address()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var created = await SavePostAsync(admin, "Ancien titre", status: "Published");
        var editUrl = created.Headers.Location!.OriginalString.Split('?')[0];

        await SavePostAsync(admin, "Nouveau titre", status: "Published", slug: "nouveau-titre", editUrl: editUrl);

        var visitor = factory.CreateClientWithoutRedirects();
        var old = await visitor.GetAsync(new Uri("/articles/ancien-titre", UriKind.Relative), CancellationToken);
        Assert.Equal(HttpStatusCode.MovedPermanently, old.StatusCode);
        Assert.Equal("/articles/nouveau-titre", old.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Deleting_a_post_also_removes_its_redirects()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);
        var created = await SavePostAsync(admin, "Éphémère", status: "Published");
        var editUrl = created.Headers.Location!.OriginalString.Split('?')[0];
        await SavePostAsync(admin, "Éphémère", status: "Published", slug: "ephemere-bis", editUrl: editUrl);
        var id = editUrl[(editUrl.LastIndexOf('/') + 1)..];

        var deleted = await AdminSession.SubmitFormAsync(admin, "/admin/posts", $"/admin/posts?handler=Delete&id={id}", []);

        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        var visitor = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri("/articles/ephemere-bis", UriKind.Relative), CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri("/articles/ephemere", UriKind.Relative), CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Preview_uses_the_sanitizing_pipeline()
    {
        var admin = await AdminSession.SignedInClientAsync(factory);

        var response = await AdminSession.SubmitFormAsync(admin, "/admin/posts/edit", "/admin/posts/edit?handler=Preview", new()
        {
            ["markdown"] = "**Gras** <script>alert(1)</script>",
        });

        var html = await response.Content.ReadAsStringAsync(CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<strong>Gras</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_requires_a_signed_in_admin()
    {
        var visitor = factory.CreateClientWithoutRedirects();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["markdown"] = "x" });

        var response = await visitor.PostAsync(new Uri("/admin/posts/edit?handler=Preview", UriKind.Relative), content, CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_home_page_is_paginated()
    {
        await using var isolated = new BlogFactory();
        await SeedPublishedPostsAsync(isolated, count: 11);
        var visitor = isolated.CreateClientWithoutRedirects();

        var first = await GetAsync(visitor, "/");
        Assert.Contains("Article 11", first, StringComparison.Ordinal); // newest first
        Assert.DoesNotContain(">Article 1<", first, StringComparison.Ordinal);
        Assert.Contains("href=\"/page/2\"", first, StringComparison.Ordinal);

        var second = await GetAsync(visitor, "/page/2");
        Assert.Contains(">Article 1<", second, StringComparison.Ordinal);
        Assert.Contains("rel=\"prev\" href=\"/\"", second, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri("/page/3", UriKind.Relative), CancellationToken)).StatusCode);
        var pageOne = await visitor.GetAsync(new Uri("/page/1", UriKind.Relative), CancellationToken);
        Assert.Equal(HttpStatusCode.MovedPermanently, pageOne.StatusCode);
        Assert.Equal("/", pageOne.Headers.Location!.OriginalString);
    }

    private static async Task<HttpResponseMessage> SavePostAsync(
        HttpClient admin, string title, string status = "Draft", string language = "French", string? slug = null,
        string body = "Contenu.", string editUrl = "/admin/posts/edit")
    {
        var fields = new Dictionary<string, string>
        {
            ["Form.Title"] = title,
            ["Form.Excerpt"] = $"Résumé de {title}.",
            ["Form.Language"] = language,
            ["Form.Status"] = status,
            ["Form.BodyMarkdown"] = body,
        };
        if (slug is not null)
        {
            fields["Form.Slug"] = slug;
        }

        return await AdminSession.SubmitFormAsync(admin, editUrl, editUrl, fields);
    }

    private static async Task SeedPublishedPostsAsync(BlogFactory blog, int count)
    {
        using var scope = blog.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        for (var n = 1; n <= count; n++)
        {
            var date = start.AddDays(n);
            db.Posts.Add(new Post
            {
                Title = $"Article {n}",
                Slug = $"article-{n}",
                Excerpt = "Résumé.",
                Status = PostStatus.Published,
                PublishedAt = date,
                CreatedAt = date,
                UpdatedAt = date,
            });
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Task<string> GetAsync(HttpClient client, string url) =>
        client.GetStringAsync(new Uri(url, UriKind.Relative), CancellationToken);
}
