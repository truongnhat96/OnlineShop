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
        private readonly IAHPRecommendationService _ahpRecommendationService;
        private const string BaseUrl = "https://router.huggingface.co/v1/chat/completions";

        public DeepSeekAI(IProductManage productManage, IHttpClientFactory httpClientFactory, IOptions<AuthorizeHFToken> authorizeHFToken, IDistributedCache cache, CacheProductOption cacheProductOption, IAHPRecommendationService ahpRecommendationService)
        {
            _productManage = productManage;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _cacheProductOption = cacheProductOption;
            _ahpRecommendationService = ahpRecommendationService;
            _cacheOption = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheProductOption.CacheLifeTime
            };
            HFToken = Environment.GetEnvironmentVariable("HF_TOKEN") ?? authorizeHFToken.Value.HFToken;
        }

        public async Task<AIResponse> AskAsync(string prompt, CancellationToken cancellationToken = default)
        {
            // AHP recommendation path
            if (IsProductRecommendationQuery(prompt))
            {
                var sessionId = Guid.NewGuid().ToString();
                var ahpText = await _ahpRecommendationService.GenerateRecommendationResponseAsync(prompt, sessionId);
                return new AIResponse
                {
                    Choices = new List<Choice>
                    {
                        new Choice
                        {
                            Message = new MessageResponse { Role = "assistant", Content = ahpText }
                        }
                    }
                };
            }

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
                        Content = @"
                    Bạn là trợ lý khách hàng của trang web truongshop.id.vn.
                    Nhiệm vụ của bạn là trả lời và tư vấn cho khách hàng dựa trên dữ liệu sản phẩm/dịch vụ được cung cấp từ cơ sở dữ liệu.

                    Nguyên tắc trả lời:
                    1. Luôn trả lời đúng trọng tâm câu hỏi của khách hàng.
                    2. Nếu các câu hỏi liên quan đến các từ khoá : Doanh nhân, giáo viên, doanh nghiệp, chất lượng cao ngay lập tức sử dụng thuật toán AHP và dữ liệu được cung cấp sẵn để đưa ra câu trả lời phù hợp.
                    3. Khi giới thiệu sản phẩm sử dụng thuật toán AHP:
                       - Chỉ đưa ra đúng 1 sản phẩm nổi bật nhất và điểm AHP đã được tính toán dựa trên kết quả thuật toán AHP đã được tích hợp.
                       - Hiển thị theo thứ tự từ cao đến thấp.
                       - Mỗi sản phẩm cần trình bày rõ ràng: Tên, Giá, Mô tả ngắn gọn, Ưu điểm nổi bật.
                       - Trình bày danh sách sản phẩm theo dạng xuống dòng dễ nhìn, bố cục gọn gàng.
                    4. Khi khách hàng chỉ yêu cầu giới thiệu sản phẩm thì hãy liệt kê 3 sản phẩm nổi bật nhất phù hợp yêu cầu khách hàng, mỗi sản phẩm cách nhau 1 dòng trắng.
                    5. Nếu khách hàng hỏi ngoài phạm vi dữ liệu (sản phẩm/dịch vụ), hãy giải thích rõ ràng:
                       'Xin lỗi, tôi chỉ là trợ lý khách hàng tại truongshop.id.vn và hiện chưa có đủ dữ liệu để trả lời câu hỏi này.'
                    6. Không chèn hình ảnh hoặc liên kết ảnh trong câu trả lời.
                    7. Luôn ưu tiên đưa ra câu trả lời thân thiện, hữu ích và dễ hiểu cho khách hàng.
                    Quan trọng nhất: luôn đưa ra 1 sản phẩm duy nhất nếu áp dụng thuật toán AHP!"
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

            var payload = new AIRequest
            {
                Messages = systemMessage,
                Model = "openai/gpt-oss-120b:novita"
                //Orenguteng/Llama-3.1-8B-Lexi-Uncensored-V2:featherless-ai
                //openai/gpt-oss-120b:novita
            };

            var response = await client.PostAsync(BaseUrl, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            Console.WriteLine(await response.Content.ReadAsStringAsync(cancellationToken));
            var ai = await response.Content.ReadFromJsonAsync<AIResponse>(cancellationToken: cancellationToken) ?? new AIResponse();
            SanitizeImageLinks(ai);
            return ai;
        }

        private static void SanitizeImageLinks(AIResponse ai)
        {
            if (ai?.Choices == null) return;
            foreach (var c in ai.Choices)
            {
                if (c?.Message?.Content == null) continue;
                var text = c.Message.Content;
                // Loại bỏ mẫu: **Hình ảnh**: [Xem tại đây](...)
                var idx = text.IndexOf("Hình ảnh", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    // Xóa từng dòng có chứa "Hình ảnh"
                    var lines = text.Split('\n');
                    lines = lines.Where(l => l.IndexOf("Hình ảnh", StringComparison.OrdinalIgnoreCase) < 0).ToArray();
                    c.Message.Content = string.Join('\n', lines).Trim();
                }
            }
        }

        private bool IsProductRecommendationQuery(string prompt)
        {
            // Chỉ kích hoạt AHP khi có ý định tư vấn/xếp hạng rõ ràng
            var strongIntents = new[]
            {
                "gợi ý", "tư vấn", "nên mua", "so sánh", "phù hợp", "ưu tiên",
                "giá rẻ", "rẻ", "sinh viên", "student"
            };
            var p = prompt.ToLower();
            return strongIntents.Any(k => p.Contains(k));
        }
    }
}