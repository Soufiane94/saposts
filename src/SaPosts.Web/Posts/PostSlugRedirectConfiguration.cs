using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaPosts.Web.Posts;

internal sealed class PostSlugRedirectConfiguration : IEntityTypeConfiguration<PostSlugRedirect>
{
    public void Configure(EntityTypeBuilder<PostSlugRedirect> builder)
    {
        builder.Property(r => r.Slug).HasMaxLength(200);
        builder.HasIndex(r => r.Slug).IsUnique();

        builder.HasOne(r => r.Post)
            .WithMany()
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
