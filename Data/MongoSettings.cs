namespace GeoPolitics_Game.Data
{
    // Holds the values from appsettings.json + user secrets
    public class MongoSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }
}
