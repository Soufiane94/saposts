using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaPosts.Web.Posts;

internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.Property(p => p.Title).HasMaxLength(200);
        builder.Property(p => p.Slug).HasMaxLength(200);
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Excerpt).HasMaxLength(500);
        builder.Property(p => p.Language).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        // Home page query: published posts, newest first.
        builder.HasIndex(p => new { p.Status, p.PublishedAt });

        builder.HasOne(p => p.CoverMedia)
            .WithMany()
            .HasForeignKey(p => p.CoverMediaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
