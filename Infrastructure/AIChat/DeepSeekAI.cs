using Infrastructure.AIChat.CacheSupport;
using Infrastructure.AIChat.Model;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using UseCase.Business_Logic;

namespace Infrastructure.AIChat
{
    public class DeepSeekAI : IAIChat
    {
        private readonly IProductManage _productManage;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDistributedCache _cache;
        private readonly DistributedCacheEntryOptions _cacheOption;
        private readonly CacheProductOption _cacheProductOption;
        private readonly string HFToken;
        private const string BaseUrl = "https://router.huggingface.co/v1/chat/completions";

        public DeepSeekAI(IProductManage productManage, IHttpClientFactory httpClientFactory, IOptions<AuthorizeHFToken> authorizeHFToken, IDistributedCache cache, CacheProductOption cacheProductOption)
        {
            _productManage = productManage;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _cacheProductOption = cacheProductOption;
            _cacheOption = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheProductOption.CacheLifeTime
            };
            HFToken = Environment.GetEnvironmentVariable("HF_TOKEN") ?? authorizeHFToken.Value.HFToken;
        }

        public async Task<AIResponse> AskAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {HFToken}");

            string cacheKey = _cacheProductOption.CacheKey;
            var cachedResponse = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (string.IsNullOrEmpty(cachedResponse))
            {
                var products = await _productManage.GetProductsAsync();
                cachedResponse = JsonSerializer.Serialize(products.ToList(), new JsonSerializerOptions { WriteIndented = true });
                await _cache.SetStringAsync(cacheKey, cachedResponse, _cacheOption, cancellationToken);
            }
            List<Message> systemMessage = [
                    new() {
                        Role = "system",
                        Content = @"Bạn là trợ lý khách hàng tại trang web truongshop.id.vn.
                                    Nhiệm vụ của bạn là trả lời và tư vấn cho khách hàng dựa trên dữ liệu sản phẩm và dịch vụ đã được cung cấp.
                                    Hãy sử dụng dữ liệu được cung cấp để trả lời các câu hỏi của khách hàng một cách chính xác và hữu ích.  
                                    Trong trường hợp câu hỏi của người dùng vượt ngoài phạm vi trang web đồng nghĩa với việc dữ liệu không được cung cấp hoặc không đầy đủ dữ liệu,
                                    hãy cố gắng giải thích rằng bạn chỉ là một trợ lý khách hàng tại truongshop và không có đủ dữ liệu để giải đáp thắc mắc của người dùng, đồng thời hãy giới thiệu người dùng sử dụng AITruongShop 
                                    (một AI được huấn luyện đầy đủ dữ liệu hơn của TruongShop) để có thể trả lời tất cả các câu hỏi ngoài lề khác, hướng dẫn người dùng hỏi bằng cách thêm từ khóa ""AITruongShop"" vào bất cứ đâu trong câu hỏi, cố gắng sử dụng lời văn thật dễ hiểu và thân thiện hơn để trả lời người dùng nhé"
                    },
                    new()
                    {
                        Role = "user",
                        Content = $"Dữ liệu:\n{cachedResponse}"
                    },
                    new() {
                        Role = "user",
                        Content = $"Người dùng: {prompt}"
                    }
            ];

            if (prompt.Contains("AITruongShop", StringComparison.OrdinalIgnoreCase))
            {
                prompt = prompt.Replace("AITruongShop", string.Empty, StringComparison.OrdinalIgnoreCase);
                systemMessage.RemoveRange(0, 2);
            }

            var payload = new AIRequest
            {
                Messages = systemMessage,
                Model = "deepseek-ai/DeepSeek-V3:together"
            };

            var response = await client.PostAsync(BaseUrl, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AIResponse>(cancellationToken: cancellationToken) ?? new AIResponse();
        }
    }
}
