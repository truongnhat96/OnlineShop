using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Text.Json;
namespace Infrastructure.PaymentSupport
{
    public static class PayOSHelper
    {
        public static string CreateSignature(string data, string checksumKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            return BitConverter
                .ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)))
                .Replace("-", "")
                .ToLower();
        }

        public static async Task<string> CancelPaymentLinkAsync(long payosOrderCode, IConfiguration config)
        {
            try
            {
                var clientId = config["PayOS:ClientId"];
                var apiKey = config["PayOS:ApiKey"];
                var url = $"https://api-merchant.payos.vn/v2/payment-requests/{payosOrderCode}/cancel";

                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("x-client-id", clientId);
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);


                var content = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"--- PAYOS CANCEL RESPONSE (Order: {payosOrderCode}) ---");
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Body: {responseBody}");
                Console.WriteLine("=============================================");

                if (response.IsSuccessStatusCode)
                {
                    var jsonDoc = JsonDocument.Parse(responseBody);
                    if (jsonDoc.RootElement.TryGetProperty("code", out var codeElement))
                    {
                        return codeElement.GetString() ?? string.Empty; // Return the code (e.g., "00", "221")
                    }
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--- ERROR CANCELLING PAYOS LINK (Order: {payosOrderCode}) ---");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("==========================================================");
                return string.Empty;
            }
        }
    }

}
