using Guardou.Domain.Files;
using Guardou.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Guardou.Infrastructure.Persistence;

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    private const string ActiveFilter = "\"Trashed\" IS NULL";
    private const string RootFilter = "\"ParentId\" IS NULL";

    public const string Version = "Version";

    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");
        builder.HasKey(item => item.Id);
        builder.Ignore(item => item.IsRoot);
        builder.Ignore(item => item.IsFolder);
        builder.Property(item => item.Name).HasMaxLength(ItemName.MaximumLength);
        builder.Property(item => item.Kind).HasConversion<string>();
        builder.Property(item => item.Created).HasConversion<DateTimeOffsetToBinaryConverter>();
        builder.Property(item => item.Modified).HasConversion<DateTimeOffsetToBinaryConverter>();
        builder.Property(item => item.Trashed).HasConversion<DateTimeOffsetToBinaryConverter>();
        builder.Property<Guid>(Version).IsConcurrencyToken();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Item>().WithMany().HasForeignKey(item => item.ParentId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.ParentId, item.Name }).IsUnique().HasFilter(ActiveFilter);
        builder.HasIndex(item => item.ParentId);
        builder.HasIndex(item => item.Trashed);
        builder.HasIndex(item => new { item.CreatedBy, item.Kind });
        builder.HasIndex(item => item.OwnerId).IsUnique().HasFilter(RootFilter);
        builder.HasIndex(item => new { item.OwnerId, item.Trashed });
        builder.HasIndex(item => new { item.OwnerId, item.Starred });
    }
}
