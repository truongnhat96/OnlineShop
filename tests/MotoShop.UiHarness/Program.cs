using System.Security.Claims;
using System.Text.Json;
using Entities;
using Infrastructure.Controllers;
using Infrastructure.Models;
using Infrastructure.Models.AHP;
using Infrastructure.Models.Momo;
using Infrastructure.Models.VnPay;
using Infrastructure.PaymentSupport.Momo.Service;
using Infrastructure.PaymentSupport.VnPay.Service;
using Infrastructure.AIChat;
using Infrastructure.EditorImages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Moq;
using UseCase;
using UseCase.Business_Logic;
using UseCase.MailService;
using UseCase.UnitOfWork;

if (args.Contains("--inspect-article-content"))
{
    await ArticleContentProbe.RunAsync();
    return;
}
if (args.Contains("--cloudinary-ping") || args.Contains("--cloudinary-upload-probe"))
{
    await CloudinaryProbe.RunAsync(args.Contains("--cloudinary-upload-probe"));
    return;
}
// TEST-ONLY host. Does not call Infrastructure.Program.Main or register any DB/provider.
if (!args.Contains("--ui-fixtures"))
{
    Console.Error.WriteLine("This is a UI fixture host, not the real shop. Run: dotnet run --project Infrastructure/Infrastructure.csproj --launch-profile http");
    Console.Error.WriteLine("Only for UI automation: add -- --ui-fixtures (port 15187, mock data and test identities).");
    Environment.ExitCode = 2;
    return;
}
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var webRoot = Path.Combine(root, "Infrastructure/wwwroot");
var persistence = args.Contains("--admin-persistence");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args.Where(argument => argument != "--ui-fixtures" && argument != "--admin-persistence").ToArray(), ContentRootPath = Path.Combine(root, "Infrastructure"), WebRootPath = webRoot, EnvironmentName = "Development" });
builder.WebHost.UseUrls(persistence ? "http://127.0.0.1:15188" : "http://127.0.0.1:15187");
Console.WriteLine(persistence ? "ADMIN TESTS ONLY: real services/repositories, isolated SQLite and fixture auth at http://127.0.0.1:15188. No business DB." : "UI FIXTURES ONLY: mocked data/auth at http://127.0.0.1:15187. Real app: http://localhost:5085.");
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Infrastructure", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Error);
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.Configure<KeyManagementOptions>(options => options.XmlRepository = new MemoryKeyRepository());
builder.Configuration.AddJsonFile("storefront.json");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
    ["Cloudinary:CloudName"] = "motoshop-fixture", ["Cloudinary:ApiKey"] = "TEST-ONLY",
    ["Cloudinary:ApiSecret"] = "TEST-ONLY", ["Cloudinary:Folder"] = "motoshop/editor", ["Cloudinary:MaxImageBytes"] = "5242880"
});
builder.Services.AddEditorImages(builder.Configuration);
builder.Services.AddSingleton<FixtureCloudinaryGateway>();
builder.Services.AddSingleton<ICloudinaryGateway>(services => services.GetRequiredService<FixtureCloudinaryGateway>());
builder.Services.AddSingleton<IImageHostResolver, FixtureImageHostResolver>();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly).AddApplicationPart(typeof(FixtureViewsController).Assembly);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => {
    options.LoginPath = "/Login"; options.AccessDeniedPath = "/Forbidden";
    options.Events.OnRedirectToLogin = context => EditorImageRegistration.RedirectOrError(context.HttpContext, context.RedirectUri, 401);
    options.Events.OnRedirectToAccessDenied = context => EditorImageRegistration.RedirectOrError(context.HttpContext, context.RedirectUri, 403);
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options => {
    options.AddConcurrencyLimiter("Concurrency", limit => { limit.PermitLimit = 1000; limit.QueueLimit = 0; });
    options.AddConcurrencyLimiter("SlidingWindow", limit => { limit.PermitLimit = 1000; limit.QueueLimit = 0; });
});
var categories = new List<Category> { new() { Id = 1, Name = "Phụ kiện xe máy · DEMO" }, new() { Id = 2, Name = "Trang bị bảo hộ · DEMO" }, new() { Id = 3, Name = "Cẩm nang · DEMO" } };
var products = Enumerable.Range(1, 26).Select(id => new Product {
    Id = id, Name = id == 1 ? "Mũ bảo hiểm touring · DEMO" : id == 2 ? "Găng tay đi xe · DEMO" : id == 26 ? "Phụ kiện kiểm tra tiêu đề dài cho nhiều kích thước màn hình và bảng quản trị · DEMO" : $"Phụ kiện xe máy {id:00} · DEMO",
    Brand = "Thương hiệu mẫu", CategoryId = id % 2 + 1, Price = 250000 + id * 10000, OldPrice = 0,
    Quantity = id == 2 ? 0 : 12, Sold = id, Date_Import = new DateTime(2026, 10, 1).AddDays(id % 10),
    Description = "Dữ liệu mẫu chỉ phục vụ kiểm tra giao diện. Không phải thông tin sản phẩm kinh doanh.",
    ImageUrl = id == 3 ? "missing-fixture.jpg" : "../../../images/motoshop-hero.svg"
}).ToList();
var postId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var posts = Enumerable.Range(1, 25).Select(id => new Post { Id = id == 1 ? postId : Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}"), Title = $"Cẩm nang lựa chọn phụ kiện {id:00} · DEMO", Content = "<h2>Nội dung mẫu</h2><p>Bài viết chỉ dùng để kiểm tra giao diện.</p>", ImageUrl = "../../../images/motoshop-hero.svg", CreatedAt = new DateTime(2026, 10, 1).AddDays(id % 10), UserId = 1 }).ToList();
var pm = new Mock<IProductManage>();
pm.Setup(p => p.GetProductsAsync()).ReturnsAsync(() => products.ToList());
pm.Setup(p => p.GetProductsAsync(It.IsAny<string>())).ReturnsAsync((string keyword) => products.Where(p => p.Name.Contains(keyword ?? "", StringComparison.OrdinalIgnoreCase)).ToList());
pm.Setup(p => p.GetProductsByCategoryAsync(It.IsAny<int>())).ReturnsAsync((int id) => products.Where(p => p.CategoryId == id).ToList());
pm.Setup(p => p.GetAllCategoriesAsync()).ReturnsAsync(categories);
pm.Setup(p => p.GetProductDetail(It.IsAny<int>())).ReturnsAsync((int id) => products.FirstOrDefault(p => p.Id == id)!);
pm.Setup(p => p.FilterByPriceAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>())).ReturnsAsync((double min, double max, int id) => products.Where(p => p.CategoryId == id && p.Price >= min && (max == 0 || p.Price <= max)).ToList());
pm.Setup(p => p.Filter(It.IsAny<SortingType>(), It.IsAny<int>())).ReturnsAsync((SortingType _, int id) => products.Where(p => p.CategoryId == id).ToList());
pm.Setup(p => p.GetAvgRating(It.IsAny<int>())).ReturnsAsync((int id) => id == 1 ? 4 : 0);
pm.Setup(p => p.GetItems(It.IsAny<int>())).ReturnsAsync(new[] { new ItemInfor { Id = 1, ProductId = 1, AccountName = "DO_NOT_RENDER_FIXTURE_ACCOUNT", Password = "DO_NOT_RENDER_FIXTURE_SECRET", Key = "DO_NOT_RENDER_FIXTURE_KEY" } });
pm.Setup(p => p.GetItem(It.IsAny<int>())).ReturnsAsync(new ItemInfor { Id = 1, ProductId = 1, AccountName = "DO_NOT_RENDER_FIXTURE_ACCOUNT", Password = "DO_NOT_RENDER_FIXTURE_SECRET", Key = "DO_NOT_RENDER_FIXTURE_KEY" });
pm.Setup(p => p.GetCouponAsync(It.IsAny<int>())).ReturnsAsync((Discount?)null);
pm.Setup(p => p.AddProductAsync(It.IsAny<Product>())).ReturnsAsync((Product product) => { product.Id = products.Max(p => p.Id) + 1; products.Add(product); return product; });
pm.Setup(p => p.UpdateProductAsync(It.IsAny<Product>())).ReturnsAsync((Product product) => { var index = products.FindIndex(p => p.Id == product.Id); if (index >= 0) { product.ImageUrl ??= products[index].ImageUrl; products[index] = product; } return product; });
pm.Setup(p => p.DeleteProductAsync(It.IsAny<int>(), It.IsAny<string?>())).ReturnsAsync((int id, string? _) => { var product = products.First(p => p.Id == id); products.Remove(product); return product; });
builder.Services.AddSingleton(pm.Object);
var cm = new Mock<ICategoryManage>(); cm.Setup(c => c.GetAllCategoriesAsync()).ReturnsAsync(categories); builder.Services.AddSingleton(cm.Object);
var hm = new Mock<IHomeManage>(); hm.Setup(h => h.FindProductAsync(It.IsAny<string>())).ReturnsAsync((string keyword) => products.Where(p => p.Name.Contains(keyword ?? "", StringComparison.OrdinalIgnoreCase)).ToList()); builder.Services.AddSingleton(hm.Object);
var fixtureReviews = new List<Review> {
    new() { UserId = 1, ProductId = 1, Rating = 3, Comment = "Nhận xét mẫu để kiểm tra hiển thị ba sao và xuống dòng.\nDòng nhận xét tiếp theo.", CreatedAt = new DateTime(2026, 10, 1) },
    new() { UserId = 2, ProductId = 1, Rating = 5, Comment = "Nhận xét mẫu để kiểm tra hiển thị năm sao.", CreatedAt = new DateTime(2026, 10, 2) }
};
var reviews = new Mock<IReviewerFinder>();
reviews.Setup(r => r.GetReview(It.IsAny<int>())).ReturnsAsync((int id) => fixtureReviews.Where(review => review.ProductId == id));
reviews.Setup(r => r.GetUserName(It.IsAny<int>())).ReturnsAsync((int id) => id == 1 ? "Khách hàng mẫu · DEMO" : "Khách hàng có tên dài để kiểm tra giao diện trên màn hình nhỏ · DEMO");
reviews.Setup(r => r.AddReviewAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>())).ReturnsAsync((int userId,int productId,int rating,string? comment) => new Review { UserId=userId,ProductId=productId,Rating=rating,Comment=comment });
builder.Services.AddSingleton(reviews.Object);
var memberCart = new List<CartItem> { new() { Id = Guid.NewGuid(), ProductId = 1, UserId = 1, Quantity = 1 } };
var cart = new Mock<ICartManage>();
cart.Setup(c => c.GetCartItemsAsync(It.IsAny<int>())).ReturnsAsync(() => memberCart.ToList());
cart.Setup(c => c.GetProductInCartAsync(It.IsAny<int>())).ReturnsAsync((int id) => products.First(p => p.Id == id));
cart.Setup(c => c.GetProductInCartAfterDiscountAsync(It.IsAny<int>(), It.IsAny<string>())).ThrowsAsync(new Exception("Fixture: coupon unavailable."));
cart.Setup(c => c.IsCouponUsed(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(false);
cart.Setup(c => c.UpdateCartItemAsync(It.IsAny<int>(),It.IsAny<int>(),It.IsAny<int>())).Returns((int productId,int _,int quantity) => { memberCart.First(c=>c.ProductId==productId).Quantity=quantity; return Task.CompletedTask; });
cart.Setup(c => c.RemoveCartItemAsync(It.IsAny<int>(),It.IsAny<int>())).Returns((int productId,int _) => { memberCart.RemoveAll(c=>c.ProductId==productId); return Task.CompletedTask; });
cart.Setup(c => c.OrderProcessingAsync(It.IsAny<int>(),It.IsAny<int>())).Returns(Task.CompletedTask);
builder.Services.AddSingleton(cart.Object);
var user = new User { Id=1,Username="fixture",DisplayName="Khách mẫu DEMO",Email="fixture@example.invalid",Password="fixture-only",RoleId=2 };
var users = new Mock<IUserManage>(); users.Setup(u=>u.GetUserAsync(It.IsAny<int>())).ReturnsAsync(user); users.Setup(u=>u.GetUserAsync(It.IsAny<string>())).ReturnsAsync(user); users.Setup(u=>u.LoginAsync(It.IsAny<string>(),It.IsAny<string>())).ThrowsAsync(new ArgumentNullException()); users.Setup(u=>u.LoginAsync("fixture","fixture-only")).ReturnsAsync(user); users.Setup(u=>u.GetRoleNameAsync(It.IsAny<int>())).ReturnsAsync("User"); users.Setup(u=>u.Validate(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<bool>())).ReturnsAsync(UseCase.ValidationResult.UserExists); builder.Services.AddSingleton(users.Object);
var articles = new Mock<IPostManage>(); articles.Setup(p=>p.GetPostsAsync()).ReturnsAsync(()=>posts.ToList()); articles.Setup(p=>p.GetPostsAsync(It.IsAny<string>())).ReturnsAsync((string keyword)=>posts.Where(p=>p.Title.Contains(keyword,StringComparison.OrdinalIgnoreCase)).ToList()); articles.Setup(p=>p.GetPostDetailAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id)=>posts.First(p=>p.Id==id)); articles.Setup(p=>p.AddPostAsync(It.IsAny<int>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>())).ReturnsAsync((int uid,string title,string content,string image)=>new Post { Title=title,Content=content,ImageUrl=image,UserId=uid }); articles.Setup(p=>p.UpdatePostAsync(It.IsAny<Post>())).ReturnsAsync((Post post)=>post); articles.Setup(p=>p.DeletePostAsync(It.IsAny<Guid>(),It.IsAny<string?>())).ReturnsAsync((Guid id,string? _)=>{var post=posts.First(p=>p.Id==id);posts.Remove(post);return post;}); builder.Services.AddSingleton(articles.Object);
var sentMails = new List<MailContent>();
var failMail = false;
var mail = new Mock<IMailSender>();
mail.Setup(m=>m.SendMailAsync(It.IsAny<MailContent>())).Callback<MailContent>(message => sentMails.Add(message)).ReturnsAsync(() => !failMail);
builder.Services.AddSingleton(mail.Object);
var vnpay = new Mock<IVnPayService>(); vnpay.Setup(v=>v.PaymentExecute(It.IsAny<IQueryCollection>())).Returns((IQueryCollection query)=>new PaymentResponseModel { Success=query["fixture"]=="success",TransactionId=query["fixture"]=="success"?"DEMO-TXN":"0",OrderId=query["fixture"]=="success"?"DEMO-ORDER":"",OrderDescription=query["fixture"]=="malformed"?"missing-amount": "Thanh toán thử-260000" }); vnpay.Setup(v=>v.CreatePaymentUrl(It.IsAny<PaymentInformationModel>(),It.IsAny<HttpContext>())).Returns("/_fixture/provider"); builder.Services.AddSingleton(vnpay.Object);
var momo = new Mock<IMomoService>(); momo.Setup(m=>m.PaymentExecute(It.IsAny<IQueryCollection>())).Returns((IQueryCollection query)=>new MomoExecuteResponseModel { Amount=query["fixture"]=="malformed"?"invalid":"260000",OrderId=query["fixture"]=="malformed"?"":"DEMO-ORDER",OrderInfo="Giao dịch thử UI" }); momo.Setup(m=>m.CreatePaymentAsync(It.IsAny<OrderInfoModel>())).ReturnsAsync(new MomoCreatePaymentResponseModel { PayUrl="/_fixture/provider" }); builder.Services.AddSingleton(momo.Object);
builder.Services.AddSingleton(new Mock<IAHPService>().Object);
builder.Services.AddSingleton(new Mock<IAHPRecommendationService>().Object);
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
// Chat tests use the real HTTP controller with an explicitly test-only provider.
var chat = new Mock<IAIChat>();
chat.Setup(c => c.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string prompt, CancellationToken cancellation) =>
{
    if (prompt == "[fixture:unconfigured]") throw new ChatUnavailableException();
    if (prompt == "[fixture:provider-error]") throw new HttpRequestException("PRIVATE_PROVIDER_ERROR");
    if (prompt == "[fixture:provider-billing]") throw new HttpRequestException("PRIVATE_PROVIDER_BILLING_DETAIL", null, System.Net.HttpStatusCode.PaymentRequired);
    if (prompt == "[fixture:provider-auth]") throw new HttpRequestException("PRIVATE_PROVIDER_AUTH_DETAIL", null, System.Net.HttpStatusCode.Unauthorized);
    if (prompt == "[fixture:provider-rate]") throw new HttpRequestException("PRIVATE_PROVIDER_RATE_DETAIL", null, System.Net.HttpStatusCode.TooManyRequests);
    if (prompt == "[fixture:provider-blocked]") throw new ChatProviderException("prompt_blocked", 422, false, 200);
    if (prompt == "[fixture:provider-empty]") return Task.FromResult(new Infrastructure.AIChat.Model.AIResponse { Choices = [] });
    return Task.FromResult(new Infrastructure.AIChat.Model.AIResponse { Choices = [new() { Message = new() { Role = "assistant", Content = "<b>TEST FIXTURE ONLY</b><br>Phản hồi kiểm tra chat: " + System.Net.WebUtility.HtmlEncode(prompt) } }] });
});
builder.Services.AddSingleton(chat.Object);
if (persistence) AdminPersistence.Register(builder.Services);
var app = builder.Build();
if (persistence) await AdminPersistence.Seed(app.Services);
var fixtureRoot = Path.Combine(root, persistence ? "tests/MotoShop.UiHarness/.fixture-persistence-wwwroot" : "tests/MotoShop.UiHarness/.fixture-wwwroot");
Directory.CreateDirectory(fixtureRoot);
Directory.CreateDirectory(Path.Combine(fixtureRoot, "images"));
File.Copy(Path.Combine(webRoot, "images", "1.png"), Path.Combine(fixtureRoot, "images", "1.png"), overwrite: true);
app.Environment.WebRootPath = fixtureRoot;
app.Environment.WebRootFileProvider = new CompositeFileProvider(new PhysicalFileProvider(fixtureRoot), new PhysicalFileProvider(webRoot));
app.UseStaticFiles(new StaticFileOptions { FileProvider = app.Environment.WebRootFileProvider });
app.UseRouting(); app.UseSession(); app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization();
app.Use(async(context,next)=> { context.Response.Headers["X-MotoShop-Test-Fixture"]="true"; await next(); });
if (persistence) AdminPersistence.MapState(app);
app.MapGet("/_fixture/identity/{role}",async (string role,HttpContext context)=> {
    if(role=="guest") await context.SignOutAsync();
    else await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(new[] {new Claim(ClaimTypes.Sid,role.StartsWith("admin-") ? role[6..] : "1"),new Claim(ClaimTypes.Name,"DEMO "+role),new Claim(ClaimTypes.Email,"fixture@example.invalid"),new Claim(ClaimTypes.Role,role.StartsWith("admin")?"Admin":"User")},CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Redirect("/");
});
app.MapGet("/_fixture/cart/{state}",(string state,HttpContext context)=> {
    context.Session.SetString(CartController.CartSessionKey,JsonSerializer.Serialize(state=="empty"?new List<CartModel>():new List<CartModel>{new(){Product=products[0],Quantity=1}}));
    return Results.Redirect("/Cart");
});
app.MapGet("/_fixture/provider",()=>Results.Text("TEST FIXTURE: no external payment request was sent."));
app.MapGet("/_fixture/mail/stats", () => Results.Json(sentMails));
app.MapGet("/_fixture/mail/reset", () => { sentMails.Clear(); failMail = false; return Results.Ok(); });
app.MapGet("/_fixture/mail/fail", () => { failMail = true; return Results.Ok(); });
app.MapGet("/_fixture/cloudinary/mode/{mode}", (string mode, FixtureCloudinaryGateway gateway) => { gateway.Mode = mode; return Results.Ok(); });
app.MapGet("/_fixture/cloudinary/stats", (FixtureCloudinaryGateway gateway) => Results.Json(new { gateway.Uploads, gateway.Deletes, gateway.MaximumConcurrent }));
app.MapGet("/_fixture/cloudinary/config/{enabled}", (bool enabled, Microsoft.Extensions.Options.IOptions<EditorImageOptions> settings) => { settings.Value.ApiKey = enabled ? "TEST-ONLY" : ""; return Results.Ok(); });
app.MapGet("/_fixture/editor/source/{kind}", (string kind) => {
    var dataImage = "<img alt='Ảnh Base64 cũ' src='data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(webRoot, "images/1.png"))) + "'>";
    posts[0].Content = kind == "legacy" ? "<p>Nội dung bài cũ.</p>" + dataImage : "<p>Nội dung mẫu.</p>";
    products[0].Description = kind == "legacy" ? EditorContentService.ProductPrefix + "<p>Mô tả cũ.</p>" + dataImage : "Mô tả nguyên bản <tag> & ký tự đặc biệt.\nDòng thứ hai.";
    return Results.Ok();
});
app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
app.Run();

[Route("_fixture/view")]
public sealed class FixtureViewsController : Controller
{
    [HttpGet("qr")] public IActionResult Qr()=>View("~/Views/Payment/GenerateQrCode.cshtml");
    [HttpGet("order")] public IActionResult Order()=>View("~/Views/Payment/Order.cshtml",260000d);
    [HttpGet("confirm")] public IActionResult Confirm()=>View("~/Views/Auth/Confirm.cshtml",(object)"fixture@example.invalid");
    [HttpGet("error")] public IActionResult Error()=>View("~/Views/Shared/Error.cshtml",new ErrorViewModel {RequestId="DEMO-REQUEST"});
}

public sealed class MemoryKeyRepository : IXmlRepository
{
    private readonly List<XElement> elements = [];
    public IReadOnlyCollection<XElement> GetAllElements() => elements.Select(element => new XElement(element)).ToList();
    public void StoreElement(XElement element, string friendlyName) => elements.Add(new XElement(element));
}
