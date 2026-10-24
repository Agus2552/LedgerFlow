using LedgerFlow.Modules.Wallets.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerFlow.Modules.Wallets.Infrastructure;

public sealed class WalletsDbContext(DbContextOptions<WalletsDbContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var wallet = modelBuilder.Entity<Wallet>();
        wallet.ToTable("wallets", "wallets", table =>
        {
            table.HasCheckConstraint("ck_wallets_id", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_wallets_owner", "owner_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_wallets_currency", "currency = 'USD'");
            table.HasCheckConstraint("ck_wallets_status", "status IN ('Active', 'Closed')");
        });
        wallet.HasKey(w => w.Id);
        wallet.Property(w => w.Id).HasColumnName("id").ValueGeneratedNever();
        wallet.Property(w => w.OwnerId).HasColumnName("owner_id").IsRequired();
        wallet.Property(w => w.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        wallet.Property(w => w.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        wallet.HasIndex(w => w.OwnerId).HasDatabaseName("ix_wallets_owner_id");
    }
}
