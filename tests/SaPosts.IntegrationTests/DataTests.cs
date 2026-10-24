using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SaPosts.Web.Data;
using SaPosts.Web.Posts;

namespace SaPosts.IntegrationTests;

public sealed class DataTests(BlogFactory factory) : IClassFixture<BlogFactory>
{
    [Fact]
    public async Task Dates_are_read_back_as_utc()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var publishedAt = new DateTime(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc);
        int id;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var post = new Post { Title = "Dates", Slug = "dates", PublishedAt = publishedAt, CreatedAt = publishedAt, UpdatedAt = publishedAt };
            db.Posts.Add(post);
            await db.SaveChangesAsync(cancellationToken);
            id = post.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var post = await db.Posts.SingleAsync(p => p.Id == id, cancellationToken);

            Assert.Equal(DateTimeKind.Utc, post.CreatedAt.Kind);
            Assert.Equal(DateTimeKind.Utc, post.PublishedAt!.Value.Kind);
            Assert.Equal(publishedAt, post.PublishedAt);
        }
    }
}
