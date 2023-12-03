namespace OrderEntry
{
    public class MindfulTraderSettings
    {
        public required string Username { get; set; }

        public required string Password { get; set; }
    }

    public class DatabaseSettings
    {
        public required string Host { get; set; }

        public required int Port { get; set; }

        public required string Database { get; set; }

        public required string Username { get; set; }

        public required string Password { get; set; }
    }

    public class CharlesSchwabSettings
    {
        public required string ConsumerKey { get; set; }
    }

    public class InteractiveBrokersSettings
    {
        public required string AccountId { get; set; }

        public required int Port { get; set; }
    }
}

