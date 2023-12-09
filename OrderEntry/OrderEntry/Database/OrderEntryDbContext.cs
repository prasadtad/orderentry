using Microsoft.EntityFrameworkCore;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    public class OrderEntryDbContext(DbContextOptions<OrderEntryDbContext> options) : DbContext(options)
    {
        public DbSet<ParseSetting> ParseSettings { get; set; }

        public DbSet<StockOrder> StockOrders { get; set; }

        public DbSet<OptionOrder> OptionOrders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasPostgresEnum<Modes>()
                                                                                          .HasPostgresEnum<Strategies>()
                                                                                          .HasPostgresEnum<ParseTypes>()
                                                                                          .HasPostgresEnum<OptionTypes>();
    }
}
