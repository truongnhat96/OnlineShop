using UseCase.MailService;

namespace Infrastructure
{
    public static class MailServiceExtension
    {
        public static IServiceCollection AddMailService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MailSettings>(configuration.GetSection("MailSettings"));
            services.AddTransient<IMailSender, MailSender>();
            return services;
        }
    }
}
