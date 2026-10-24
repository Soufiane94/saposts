using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Contact;
using SaPosts.Web.Media;
using SaPosts.Web.Posts;

namespace SaPosts.Web.Data;

// IdentityUserContext rather than IdentityDbContext: a single admin needs no role tables.
public sealed class BlogDbContext(DbContextOptions<BlogDbContext> options) : IdentityUserContext<IdentityUser>(options)
{
    public DbSet<Post> Posts => Set<Post>();

    public DbSet<PostSlugRedirect> PostSlugRedirects => Set<PostSlugRedirect>();

    public DbSet<MediaItem> Media => Set<MediaItem>();

    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new PostConfiguration());
        builder.ApplyConfiguration(new PostSlugRedirectConfiguration());
        builder.ApplyConfiguration(new MediaItemConfiguration());
        builder.ApplyConfiguration(new ContactMessageConfiguration());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no date type and EF cannot order or compare DateTimeOffset there,
        // so every date is a UTC DateTime, read back with Kind = Utc.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }
}
