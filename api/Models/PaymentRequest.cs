namespace MonoPayAggregator.Models
{
    /// <summary>
    /// Represents a request to create a payment. This is a simplified model
    /// intended for demonstration. A real implementation would include more
    /// validations, allowed values and nested types.
    /// </summary>
    public class PaymentRequest
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "LSL";
        public string PaymentMethod { get; set; } = string.Empty;
        public CustomerInfo Customer { get; set; } = new CustomerInfo();
        /// <summary>
        /// Identifier of the merchant receiving the funds. This should map
        /// to a merchant account configured in the MonoPay system.
        /// </summary>
        public string MerchantId { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? CallbackUrl { get; set; }
    }

    public class CustomerInfo
    {
        public string Phone { get; set; } = string.Empty;
        public string? Name { get; set; }
    }
}