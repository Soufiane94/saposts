using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaPosts.Web.Media;

internal sealed class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("Media");
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.OriginalFileName).HasMaxLength(255);
        builder.Property(m => m.ContentType).HasMaxLength(100);
        builder.Property(m => m.AltText).HasMaxLength(500);
        builder.HasIndex(m => m.UploadedAt);

        builder.HasOne(m => m.Poster)
            .WithMany()
            .HasForeignKey(m => m.PosterId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
