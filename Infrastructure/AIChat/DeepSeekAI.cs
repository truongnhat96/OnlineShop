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
<<<<<<< HEAD
                        Content = @"Bạn là trợ lý khách hàng tại trang web truongshop.id.vn.
                                    Nhiệm vụ của bạn là trả lời và tư vấn cho khách hàng dựa trên dữ liệu sản phẩm và dịch vụ đã được cung cấp.
=======
                          Content = @"Bạn là trợ lý khách hàng tại trang web truongshop.id.vn.
                                    Nhiệm vụ của bạn là trả lời và tư vấn cho khách hàng dựa trên dữ liệu sản phẩm và áp dụng thuật toán AHP với các tiêu chí phù hợp tương ứng với sản phẩm và từ khoá mà người dùng cung cấp và dịch vụ đã được cung cấp.
                                    ĐỪNG CHO ĐIỂM AHP VÀO PHẦN TRẢ LỜI!
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
                                    Hãy cố gắng sao để trả lời giống với một trợ lý khách hàng nhất có thể,
                                    đồng thời hãy cố gắng TRÌNH BÀY TÊN MỖI SẢN PHẨM THÀNH MỘT DÒNG THAY VÌ TÊN SẢN PHẨM LẠI BỊ DÍNH CÙNG MỘT DÒNG VỚI MỤC KHÁC MÀ KHÔNG XUỐNG DÒNG (ở đầu tên sản phẩm nên có một icon minh họa sản phầm để dễ hình dung) VÀ IN ĐẬM TÊN SẢN PHẨM ĐỂ LÀM NỔI BẬT, lưu ý: không được dùng ký hiệu ""**"" tại mỗi sản phẩm khi trả lời.
                                    Trong trường hợp câu hỏi của người dùng vượt ngoài phạm vi trang web đồng nghĩa với việc dữ liệu không được cung cấp hoặc không đầy đủ dữ liệu,
                                    hãy cố gắng giải thích rằng bạn chỉ là một trợ lý khách hàng tại truongshop và không có đủ dữ liệu để giải đáp thắc mắc của người dùng"
                    },
<<<<<<< HEAD
=======

>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
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
<<<<<<< HEAD
                "gợi ý", "tư vấn", "nên mua", "so sánh", "phù hợp", "ưu tiên",
=======
                "gợi ý", "tư vấn", "nên mua", "so sánh", "ưu tiên",
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
                "giá rẻ", "rẻ", "sinh viên", "student"
            };
            var p = prompt.ToLower();
            return strongIntents.Any(k => p.Contains(k));
        }
    }
<<<<<<< HEAD
}
=======
}
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
