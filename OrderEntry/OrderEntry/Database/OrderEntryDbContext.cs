using Microsoft.EntityFrameworkCore;
using OrderEntry.Brokerages;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    public class OrderEntryDbContext(DbContextOptions<OrderEntryDbContext> options) : DbContext(options)
    {
        public DbSet<ParseSetting> ParseSettings { get; set; }

        public DbSet<StockOrder> StockOrders { get; set; }

        public DbSet<OptionOrder> OptionOrders { get; set; }

        public DbSet<InteractiveBrokersStock> InteractiveBrokersStocks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasPostgresEnum<Modes>()
                                                                                          .HasPostgresEnum<Strategies>()
                                                                                          .HasPostgresEnum<ParseTypes>()
                                                                                          .HasPostgresEnum<OptionTypes>()
                                                                                          .HasPostgresEnum<Brokers>();
    }
}
