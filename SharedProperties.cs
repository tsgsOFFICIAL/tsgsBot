namespace tsgsBot_C_
{
    public sealed class SharedProperties
    {
        private static readonly Lazy<SharedProperties> _instance = new(() => new SharedProperties());
        public static SharedProperties Instance => _instance.Value;

        private static DateTimeOffset _uptime = DateTimeOffset.UtcNow;
        public DateTimeOffset UpTime
        {
            get => _uptime;
            set => _uptime = value;
        }

        public string STEAM_WEB_API_KEY { get; set; } = string.Empty;
        public string STEAM_API_KEY { get; set; } = string.Empty;

        private SharedProperties()
        {
            UpTime = DateTimeOffset.UtcNow;
        }

        public void Initialize()
        {
            STEAM_API_KEY = Environment.GetEnvironmentVariable("STEAM_API_KEY") ?? string.Empty;
            STEAM_WEB_API_KEY = Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY") ?? string.Empty;
        }
    }
}
