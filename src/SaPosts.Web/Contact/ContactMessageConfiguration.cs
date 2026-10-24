using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaPosts.Web.Contact;

internal sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.Property(m => m.Name).HasMaxLength(100);
        builder.Property(m => m.Email).HasMaxLength(254);
        builder.Property(m => m.Message).HasMaxLength(5000);
        builder.Property(m => m.LastEmailError).HasMaxLength(1000);

        // Inbox order and retention purge.
        builder.HasIndex(m => m.ReceivedAt);
    }
}
