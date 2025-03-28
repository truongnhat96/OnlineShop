using Infrastructure.Models.VnPay;

namespace Infrastructure.PaymentSupport.VnPay.Service
{
    public interface IVnPayService
    {
       public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context);
       public PaymentResponseModel PaymentExecute(IQueryCollection collections);

    }
}
