using Infrastructure.Models;
using Infrastructure.Models.Momo;

namespace Infrastructure.PaymentSupport.Momo.Service
{
    public interface IMomoService
    {
        public Task<MomoCreatePaymentResponseModel?> CreatePaymentAsync(OrderInfoModel model);
        public MomoExecuteResponseModel PaymentExecute(IQueryCollection collection);
    }
}
