using Microsoft.Extensions.Options;
using Npgsql;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    public class DatabaseService : IDatabaseService
	{
		private readonly IOptions<DatabaseSettings> options;

		public DatabaseService(IOptions<DatabaseSettings> options)
		{
			this.options = options;
		}

		public async Task<List<ParseSetting>> GetParseSettings()
        {
            var parseSettings = new List<ParseSetting>();

            using (var conn = await OpenConnection())
            using (var cmd = new NpgsqlCommand("SELECT * FROM parsesetting", conn))
            using (var reader = cmd.ExecuteReader())
            while (await reader.ReadAsync())
            {
                parseSettings.Add(new ParseSetting
                {
                    Key = Read<string>(reader, nameof(ParseSetting.Key))!,
                    AccountBalance = Read<decimal>(reader, nameof(ParseSetting.AccountBalance)),
                    ParseType = Read<ParseTypes>(reader, nameof(ParseSetting.ParseType)),
                    Mode = Read<Modes>(reader, nameof(ParseSetting.Mode)),
                    Strategy = Read<Strategies>(reader, nameof(ParseSetting.Strategy))
                });
            }

            return parseSettings;
		}

        private static T? Read<T>(NpgsqlDataReader reader, string field)
        {
            var ordinal = reader.GetOrdinal(field);
            if (reader.IsDBNull(ordinal)) return default;

            if (typeof(T).IsEnum)
            {
                var value = reader.GetString(ordinal);
                return (T) Enum.Parse(typeof(T), value, true);
            }
            return reader.GetFieldValue<T>(ordinal);
        }

        private async Task<NpgsqlConnection> OpenConnection()
		{
			var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder
            {                
                Pooling = true,
                SslMode = SslMode.VerifyFull,
                Host = options.Value.Host,
                Port = options.Value.Port,
                Username = options.Value.Username,
                Password = options.Value.Password,
                Database = options.Value.Database
            }.ConnectionString);

			await conn.OpenAsync();

			return conn;
        }
    }

	public interface IDatabaseService
	{
        Task<List<ParseSetting>> GetParseSettings();
	}
}

