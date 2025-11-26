namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Configuration options for a payment provider. Each provider can define
    /// additional properties as needed (e.g. API keys, base URLs).
    /// These values are loaded from appsettings.json via the options pattern.
    /// </summary>
    public class ProviderConfig
    {
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}