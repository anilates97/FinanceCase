using FinanceCase.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceCase.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<ExchangeRate> ExchangeRates { get; set; }
    public DbSet<AssetRecord> AssetRecords { get; set; }
    public DbSet<InflationIndexRecord> InflationIndexRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ExchangeRate>()
            .HasIndex(x => new { x.BaseCurrencyCode, x.ForeignCurrencyCode, x.CurrentDate })
            .IsUnique();

        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.ChangeRate)
            .HasPrecision(18, 6);

        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.ExchangeRateValue)
            .HasPrecision(18, 6);

        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.CashChangeRate)
            .HasPrecision(18, 6);

        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.CashExchangeRate)
            .HasPrecision(18, 6);

        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.CentralBankChangeRate)
            .HasPrecision(18, 6);

        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.CentralBankExchangeRate)
            .HasPrecision(18, 6);

        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.CrossRate)
            .HasPrecision(18, 6);

        modelBuilder.Entity<AssetRecord>()
            .HasIndex(x => x.Period)
            .IsUnique();

        modelBuilder.Entity<AssetRecord>()
            .Property(x => x.AssetAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InflationIndexRecord>()
            .HasIndex(x => x.Period)
            .IsUnique();

        modelBuilder.Entity<InflationIndexRecord>()
            .Property(x => x.IndexValue)
            .HasPrecision(18, 2);
    }
}
