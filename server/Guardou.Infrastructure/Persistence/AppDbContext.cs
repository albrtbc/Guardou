using Guardou.Domain.Files;
using Guardou.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Guardou.Infrastructure.Persistence;

internal sealed class AppDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        SavingChanges += (_, _) => StampItemVersions();
    }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Ignore<IdentityUserToken<string>>();
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private void StampItemVersions()
    {
        foreach (EntityEntry<Item> entry in ChangeTracker.Entries<Item>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property<Guid>(ItemConfiguration.Version).CurrentValue = Guid.NewGuid();
            }
        }
    }
}
