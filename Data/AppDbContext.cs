using System;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShopWalletApp.Models;

namespace ShopWalletApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<MasterNumber> MasterNumbers => Set<MasterNumber>();

        public const string AlertThresholdKey = "AlertThresholdAmount";
        public DbSet<AppSettings> AppSettings => Set<AppSettings>();

        public decimal GetAlertThreshold()
        {
            string? value = AppSettings.AsNoTracking()
                .Where(s => s.Key == AlertThresholdKey).Select(s => s.Value).FirstOrDefault();
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture,
                out decimal threshold) && threshold >= 0m ? threshold : 1000m;
        }

        public void SetAlertThreshold(decimal value)
        {
            if (value < 0m) throw new ArgumentOutOfRangeException(nameof(value));
            var setting = AppSettings.Find(AlertThresholdKey);
            if (setting == null)
            {
                setting = new AppSettings { Key = AlertThresholdKey };
                AppSettings.Add(setting);
            }
            setting.Value = value.ToString(CultureInfo.InvariantCulture);
            SaveChanges();
        }

        public MasterUsage GetMasterUsage(string sourcePhone, DateTime date)
        {
            DateTime today = date.Date;
            DateTime tomorrow = today.AddDays(1);
            // Preserve the existing Sunday-start calendar week.
            DateTime weekStart = today.AddDays(-(int)today.DayOfWeek);
            DateTime weekEnd = weekStart.AddDays(7);
            DateTime monthStart = new DateTime(today.Year, today.Month, 1);
            DateTime monthEnd = monthStart.AddMonths(1);
            var transactions = Transactions.Where(t => t.SourcePhone == sourcePhone &&
                (t.TransactionType == "Deposit" || t.TransactionType == "Withdraw"));
            // EF Core 8 SQLite cannot aggregate decimal in SQL. Keep exact decimal arithmetic.
            return new MasterUsage
            {
                Daily = transactions.Where(t => t.TransactionDate >= today && t.TransactionDate < tomorrow)
                    .Select(t => t.Amount).AsEnumerable().Sum(),
                Weekly = transactions.Where(t => t.TransactionDate >= weekStart && t.TransactionDate < weekEnd)
                    .Select(t => t.Amount).AsEnumerable().Sum(),
                Monthly = transactions.Where(t => t.TransactionDate >= monthStart && t.TransactionDate < monthEnd)
                    .Select(t => t.Amount).AsEnumerable().Sum()
            };
        }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured) return;

            string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ShopWalletDB.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var settings = modelBuilder.Entity<AppSettings>();
            settings.ToTable("AppSettings");
            settings.HasKey(s => s.Key);
            settings.Property(s => s.Key).HasMaxLength(100);
            settings.Property(s => s.Value).HasMaxLength(100).IsRequired();
            settings.HasData(new AppSettings { Key = AlertThresholdKey, Value = "1000" });

            var transactions = modelBuilder.Entity<Transaction>();
            transactions.ToTable("Transactions");
            transactions.HasKey(t => t.Id);
            transactions.Property(t => t.PhoneNumber).HasMaxLength(11).IsRequired();
            // Keep legacy transactions without a recorded source readable.
            transactions.Property(t => t.SourcePhone).HasMaxLength(11).IsRequired(false);
            transactions.Property(t => t.TransactionType).HasMaxLength(20).IsRequired();
            transactions.Property(t => t.Amount).HasPrecision(18, 2);
            transactions.Property(t => t.TransactionDate).IsRequired();
            transactions.HasIndex(t => new { t.SourcePhone, t.TransactionType, t.TransactionDate });

            var masters = modelBuilder.Entity<MasterNumber>();
            masters.ToTable("MasterNumbers");
            masters.HasKey(m => m.PhoneNumber);
            masters.Property(m => m.PhoneNumber).HasMaxLength(11).IsRequired();
            masters.Property(m => m.DailyLimit).HasPrecision(18, 2);
            masters.Property(m => m.WeeklyLimit).HasPrecision(18, 2);
            masters.Property(m => m.MonthlyLimit).HasPrecision(18, 2);
        }
    }
}
