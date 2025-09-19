using Infrastructure.AIChat.CacheSupport;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using UseCase.Business_Logic;

namespace Infrastructure.AIChat
{
    public static class AIChatExtension
    {
        public static IServiceCollection AddChatbot(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AuthorizeHFToken>(configuration.GetSection("AuthorizeHFToken"));
            services.AddHttpClient();
            services.AddScoped<IAIChat>(service =>
                new DeepSeekAI(
                service.GetRequiredService<IProductManage>(),
                service.GetRequiredService<IHttpClientFactory>(),
                service.GetRequiredService<IOptions<AuthorizeHFToken>>(),
                service.GetRequiredService<IDistributedCache>(),
                new CacheProductOption(),
                service.GetRequiredService<IAHPRecommendationService>() // 👈 thêm cái này
    ));

            return services;
        }
    }
}
