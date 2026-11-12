using Guardou.Domain.Files;
using Guardou.Domain.Shares;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Guardou.Infrastructure.Persistence;

internal sealed class ShareConfiguration : IEntityTypeConfiguration<Share>
{
    public void Configure(EntityTypeBuilder<Share> builder)
    {
        builder.ToTable("Shares");
        builder.HasKey(share => share.Id);
        builder.Property(share => share.Token).HasMaxLength(ShareToken.Length);
        builder.Property(share => share.Created).HasConversion<DateTimeOffsetToBinaryConverter>();
        builder.Property(share => share.Expires).HasConversion<DateTimeOffsetToBinaryConverter>();

        builder.HasOne<Item>().WithMany().HasForeignKey(share => share.ItemId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(share => share.ItemId).IsUnique();
        builder.HasIndex(share => share.Token).IsUnique();
    }
}
