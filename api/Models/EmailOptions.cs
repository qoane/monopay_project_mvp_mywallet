namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Configuration options for sending emails via SMTP.
    /// These values are bound from appsettings.json.
    /// </summary>
    public class EmailOptions
    {
        /// <summary>
        /// Address of the SMTP server (e.g. smtp.gmail.com).
        /// </summary>
        public string SmtpServer { get; set; } = string.Empty;

        /// <summary>
        /// Port to connect on (e.g. 587 for TLS).
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Friendly name used in the From header.
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// Email address used in the From header.
        /// </summary>
        public string SenderEmail { get; set; } = string.Empty;

        /// <summary>
        /// Username for authenticating with the SMTP server. If null or empty
        /// the SenderEmail will be used.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Password for authenticating with the SMTP server.
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }
}