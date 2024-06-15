using Microsoft.EntityFrameworkCore;
using OrderEntry.Apis;
using OrderEntry.Brokerages;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    public class OrderEntryDbContext(DbContextOptions<OrderEntryDbContext> options) : DbContext(options)
    {
        public DbSet<ParseSetting> ParseSettings { get; set; }

        public DbSet<StockOrder> StockOrders { get; set; }

        public DbSet<OptionOrder> OptionOrders { get; set; }

        public DbSet<StockPosition> StockPositions { get; set; }

        public DbSet<StockDayData> StockDayDatas { get; set; }

        public DbSet<MarketDate> MarketDates { get; set; }

        public DbSet<OptionContract> OptionContracts { get; set; }

        public DbSet<InsiderRecommendation> InsiderRecommendations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasPostgresEnum<Modes>()
                                                                                          .HasPostgresEnum<Strategies>()
                                                                                          .HasPostgresEnum<ParseTypes>()
                                                                                          .HasPostgresEnum<OptionTypes>()
                                                                                          .HasPostgresEnum<Brokers>()
                                                                                          .HasPostgresEnum<MarketDateTypes>();
    }
}
