using Infrastructure.Models.VnPay;
using Infrastructure.PaymentSupport.VnPay.Lib;

namespace Infrastructure.PaymentSupport.VnPay.Service
{
    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _configuration;

        public VnPayService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context)
        {
            var timeZoneById = TimeZoneInfo.FindSystemTimeZoneById(_configuration["TimeZoneId"] ?? throw new("TimeZoneId null"));
            var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneById);
            var tick = DateTime.Now.Ticks.ToString();
            var pay = new VnPayLibrary();
            var urlCallBack = _configuration["Vnpay:PaymentBackReturnUrl"];

            pay.AddRequestData("vnp_Version", _configuration["Vnpay:Version"] ?? throw new("Version null"));
            pay.AddRequestData("vnp_Command", _configuration["Vnpay:Command"] ?? throw new("Command null"));
            pay.AddRequestData("vnp_TmnCode", _configuration["Vnpay:TmnCode"] ?? throw new("TmnCode null"));
            pay.AddRequestData("vnp_Amount", ((int)model.Amount * 100).ToString());
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", _configuration["Vnpay:CurrCode"] ?? throw new("CurrCode null"));
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", _configuration["Vnpay:Locale"] ?? throw new("Locale null"));
            pay.AddRequestData("vnp_OrderInfo", $"{model.Name}. {model.OrderDescription}-{model.Amount}");
            pay.AddRequestData("vnp_OrderType", model.OrderType);
            pay.AddRequestData("vnp_ReturnUrl", urlCallBack ?? string.Empty);
            pay.AddRequestData("vnp_TxnRef", tick);

            var paymentUrl =
                pay.CreateRequestUrl(_configuration["Vnpay:BaseUrl"] ?? throw new("BaseUrl null"), _configuration["Vnpay:HashSecret"] ?? throw new("HashSecret null"));

            return paymentUrl;
        }

        public PaymentResponseModel PaymentExecute(IQueryCollection collections)
        {
            var pay = new VnPayLibrary();
            var response = pay.GetFullResponseData(collections, _configuration["Vnpay:HashSecret"] ?? throw new("HashSecret null"));

            return response;
        }


    }
}
