namespace Infrastructure.Models.PayOS
{
    public class PayOSCreateRequest
    {
        public long orderCode { get; set; }
        public int amount { get; set; }
        public string description { get; set; } = string.Empty;
        public string cancelUrl { get; set; } = string.Empty;
        public string returnUrl { get; set; } = string.Empty;
    }

    public class PayOSCreateResponse
    {
        public string code { get; set; } = string.Empty;
        public bool success { get; set; } 
        public PayOSData? data { get; set; } 
    }

    public class PayOSData
    {
        public string qrCode { get; set; } = string.Empty;
        public string accountNumber { get; set; } = string.Empty;
        public string accountName { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
    }

    public class PayOSWebhookData
    {
        public string accountNumber { get; set; } = string.Empty;
        public int amount { get; set; }
        public string description { get; set; } = string.Empty;
        public string reference { get; set; } = string.Empty;
        public string transactionDateTime { get; set; } = string.Empty;
        public string virtualAccountNumber { get; set; } = string.Empty;
        public string counterAccountBankId { get; set; } = string.Empty;
        public string counterAccountBankName { get; set; } = string.Empty;
        public object? counterAccountName { get; set; }  
        public string counterAccountNumber { get; set; } = string.Empty;
        public string virtualAccountName { get; set; } = string.Empty;
        public string currency { get; set; } = string.Empty;
        public long orderCode { get; set; }
        public string paymentLinkId { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string desc { get; set; } = string.Empty;
    }

    public class PayOSWebhookResponse
    {
        public string code { get; set; } = string.Empty;
        public string desc { get; set; } = string.Empty;
        public bool success { get; set; }
        public PayOSWebhookData? data { get; set; }
        public string signature { get; set; } = string.Empty;
    }

}
