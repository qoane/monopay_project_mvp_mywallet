namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Represents a user (merchant) of the MonoPay platform. This class
    /// contains only a few fields for demonstration. A production ready
    /// implementation would include authentication claims, roles, status and
    /// verification details.
    /// </summary>
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        /// <summary>
        /// Secure hash of the user's password. Never return this value in
        /// API responses.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;
        /// <summary>
        /// True when the user has clicked the verification link sent via
        /// email. Only verified users may log in and obtain tokens.
        /// </summary>
        public bool IsVerified { get; set; } = false;
        /// <summary>
        /// Merchant identifier for payments. For most merchants this will be
        /// automatically generated or mapped to an external wallet ID. It
        /// should be used to tag transactions and route funds to the
        /// appropriate account.
        /// </summary>
        public string MerchantId { get; set; } = string.Empty;
        /// <summary>
        /// Indicates whether the user has administrative privileges. Admin
        /// users can manage other users, providers and view reports.
        /// </summary>
        public bool IsAdmin { get; set; } = false;
    }
}