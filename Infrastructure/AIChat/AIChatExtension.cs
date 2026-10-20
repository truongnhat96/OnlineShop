using Infrastructure.AIChat.CacheSupport;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using UseCase.Business_Logic;

namespace Infrastructure.AIChat
{
    public static class AIChatExtension
    {
        public static IServiceCollection AddChatbot(this IServiceCollection services, IConfiguration configuration, string? contentRoot = null)
        {
            var localToken = contentRoot == null ? null : LocalChatSettings.ReadToken(contentRoot);
            services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));
            services.PostConfigure<GeminiOptions>(options =>
            {
                var token = configuration["GEMINI_API_KEY"];
                options.ApiKey = !string.IsNullOrWhiteSpace(token) ? token : localToken ?? options.ApiKey;
            });
            services.AddHttpClient("Gemini", client => client.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            services.AddScoped<IAIChat>(service =>
                new GeminiChat(
                service.GetRequiredService<IProductManage>(),
                service.GetRequiredService<IHttpClientFactory>(),
                service.GetRequiredService<IOptions<GeminiOptions>>(),
                service.GetRequiredService<IDistributedCache>(),
                new CacheProductOption(),
                service.GetRequiredService<IAHPRecommendationService>() // 👈 thêm cái này
    ));

            return services;
        }
    }
}
