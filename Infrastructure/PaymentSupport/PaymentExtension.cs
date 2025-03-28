using Infrastructure.Models.Momo;
using Infrastructure.PaymentSupport.Momo.Service;
using Infrastructure.PaymentSupport.VnPay.Service;

namespace Infrastructure.PaymentSupport
{
    public static class PaymentExtension
    {
        public static IServiceCollection AddPaymentService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MomoOptionModel>(configuration.GetSection("MomoAPI"));
            services.AddScoped<IMomoService, MomoService>();
            services.AddTransient<IVnPayService, VnPayService>();
            return services;
        }
    }
}
