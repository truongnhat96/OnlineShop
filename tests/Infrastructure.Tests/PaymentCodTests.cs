using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Entities;
using Infrastructure.Controllers;
using Infrastructure.Models;
using Infrastructure.PaymentSupport.Momo.Service;
using Infrastructure.PaymentSupport.VnPay.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using UseCase.Business_Logic;
using UseCase.MailService;
using Xunit;

namespace Infrastructure.Tests;

public sealed class PaymentCodTests
{
    [Fact]
    public async Task GuestCodUsesCurrentPricesSendsBothUnpaidEmailsAndClearsCartOnce()
    {
        var test = new Checkout();
        var result = Assert.IsType<RedirectToActionResult>(await test.Controller.PayByCod(test.Contact));
        Assert.Equal(nameof(PaymentController.CodConfirmed), result.ActionName);
        Assert.Null(test.Http.Session.GetString(CartController.CartSessionKey));
        Assert.Equal(2, test.Mails.Count);
        Assert.Equal(test.Contact.Email, test.Mails[0].To);
        Assert.Equal(PaymentController.adminEmail, test.Mails[1].To);
        Assert.All(test.Mails, mail => {
            Assert.Contains("COD", mail.Subject); Assert.Contains("Chưa thanh toán", mail.Body);
            Assert.Contains("Actual product", mail.Body); Assert.Contains("200.000", mail.Body);
            Assert.Contains("Test delivery address", mail.Body); Assert.DoesNotContain("chuyển khoản thành công", mail.Body);
        });
        test.Cart.Verify(cart => cart.OrderProcessingGuestAsync(It.Is<IEnumerable<CartItem>>(items => items.Single().ProductId == 1 && items.Single().Quantity == 2)), Times.Once);
        var confirmation = Assert.IsType<CodConfirmationModel>(Assert.IsType<ViewResult>(test.Controller.CodConfirmed()).Model);
        Assert.Equal(200000, confirmation.Total);
        test.Controller.CodConfirmed(); // GET/reload must not send email or process stock again.
        await test.Controller.PayByCod(test.Contact); // Repost after successful cart clear.
        Assert.Equal(2, test.Mails.Count);
    }

    [Fact]
    public async Task MemberCodUsesClaimsAndActualCouponPrice()
    {
        var test = new Checkout(member: true);
        test.Http.Session.SetString(CartController.CouponSessionKey, JsonSerializer.Serialize(new List<CouponModel> { new() { Id = 1, Name = "TEST10" } }));
        Assert.IsType<RedirectToActionResult>(await test.Controller.PayByCod(test.Contact));
        test.Cart.Verify(cart => cart.OrderProcessingAsync(42, 1), Times.Once);
        test.Cart.Verify(cart => cart.OrderProcessingGuestAsync(It.IsAny<IEnumerable<CartItem>>()), Times.Never);
        Assert.All(test.Mails, mail => Assert.Contains("180.000", mail.Body));
        Assert.Null(test.Http.Session.GetString(CartController.CouponSessionKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MailFailureOrExceptionDoesNotConfirmOrConsumeCart(bool throws)
    {
        var test = new Checkout();
        if (throws) test.Sender.Setup(sender => sender.SendMailAsync(It.IsAny<MailContent>())).ThrowsAsync(new IOException("SMTP_TEST_FAILURE"));
        else test.Sender.Setup(sender => sender.SendMailAsync(It.IsAny<MailContent>())).ReturnsAsync(false);
        var result = Assert.IsType<ViewResult>(await test.Controller.PayByCod(test.Contact));
        Assert.Equal("Option", result.ViewName);
        Assert.False(test.Controller.ModelState.IsValid);
        Assert.NotNull(test.Http.Session.GetString(CartController.CartSessionKey));
        Assert.Null(test.Http.Session.GetString(PaymentController.CodReceiptSessionKey));
        test.Cart.Verify(cart => cart.OrderProcessingGuestAsync(It.IsAny<IEnumerable<CartItem>>()), Times.Never);
        Assert.DoesNotContain("SMTP_TEST_FAILURE", string.Join(" ", test.Controller.ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task InvalidOrUnavailableStockNeverSendsEmail(int quantity)
    {
        var test = new Checkout(quantity: quantity);
        Assert.IsType<ViewResult>(await test.Controller.PayByCod(test.Contact));
        Assert.Empty(test.Mails);
        Assert.False(test.Controller.ModelState.IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OneRejectedRecipientDoesNotSilentlyConfirmCod(bool customerAccepted)
    {
        var test = new Checkout();
        test.Sender.SetupSequence(sender => sender.SendMailAsync(It.IsAny<MailContent>())).ReturnsAsync(customerAccepted).ReturnsAsync(!customerAccepted);
        Assert.IsType<ViewResult>(await test.Controller.PayByCod(test.Contact));
        Assert.NotNull(test.Http.Session.GetString(CartController.CartSessionKey));
        Assert.Null(test.Http.Session.GetString(PaymentController.CodReceiptSessionKey));
        test.Cart.Verify(cart => cart.OrderProcessingGuestAsync(It.IsAny<IEnumerable<CartItem>>()), Times.Never);
    }

    [Fact]
    public async Task InvalidFormKeepsContactAndNeverSendsMail()
    {
        var test = new Checkout();
        test.Contact.Address = "";
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.False(Validator.TryValidateObject(test.Contact, new ValidationContext(test.Contact), errors, true));
        foreach (var error in errors) test.Controller.ModelState.AddModelError(error.MemberNames.First(), error.ErrorMessage!);
        Assert.IsType<ViewResult>(await test.Controller.PayByCod(test.Contact));
        Assert.Same(test.Contact, test.Controller.ViewData["CodCheckout"]);
        Assert.Empty(test.Mails);
    }

    [Fact]
    public async Task CodEmailEncodesCustomerInput()
    {
        var test = new Checkout();
        test.Contact.Name = "<script>alert(1)</script>";
        test.Contact.Address = "<b>Test delivery address</b>";
        test.Contact.Note = "<img src=x onerror=alert(1)>";
        await test.Controller.PayByCod(test.Contact);
        Assert.All(test.Mails, mail => { Assert.DoesNotContain("<script>", mail.Body); Assert.DoesNotContain("<img src=x", mail.Body); Assert.Contains("&lt;script&gt;", mail.Body); });
    }

    private sealed class Checkout
    {
        public DefaultHttpContext Http { get; } = new() { Session = new MemorySession() };
        public Mock<ICartManage> Cart { get; } = new();
        public Mock<IMailSender> Sender { get; } = new();
        public List<MailContent> Mails { get; } = [];
        public PaymentController Controller { get; }
        public CodCheckoutModel Contact { get; } = new() { Name = "Test buyer", Email = "buyer@example.invalid", Phone = "0901234567", Address = "Test delivery address" };
        public Checkout(bool member = false, int quantity = 2)
        {
            var product = new Product { Id = 1, Name = "Actual product", Brand = "Actual", Description = "Actual", Quantity = 5, Price = 100000 };
            Cart.Setup(cart => cart.GetProductInCartAsync(1)).ReturnsAsync(product);
            Cart.Setup(cart => cart.GetProductInCartAfterDiscountAsync(1, "TEST10")).ReturnsAsync(new Product { Id = 1, Name = product.Name, Brand = product.Brand, Description = product.Description, Quantity = 5, Price = 90000 });
            Cart.Setup(cart => cart.OrderProcessingGuestAsync(It.IsAny<IEnumerable<CartItem>>())).Returns(Task.CompletedTask);
            var memberCart = new List<CartItem> { new() { ProductId = 1, UserId = 42, Quantity = quantity } };
            Cart.Setup(cart => cart.GetCartItemsAsync(42)).ReturnsAsync(() => memberCart.ToList());
            Cart.Setup(cart => cart.OrderProcessingAsync(42, It.IsAny<int>())).Callback(() => memberCart.Clear()).Returns(Task.CompletedTask);
            if (member) Http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Sid, "42")], "test"));
            else Http.Session.SetString(CartController.CartSessionKey, JsonSerializer.Serialize(new List<CartModel> { new() { Product = new Product { Id = 1, Name = "Stale product", Brand = "Stale", Description = "Stale", Price = 1 }, Quantity = quantity } }));
            Sender.Setup(sender => sender.SendMailAsync(It.IsAny<MailContent>())).Callback<MailContent>(mail => Mails.Add(mail)).ReturnsAsync(true);
            Controller = new PaymentController(Mock.Of<IVnPayService>(), Mock.Of<IMomoService>(), Cart.Object, Sender.Object) {
                ControllerContext = new ControllerContext { HttpContext = Http },
                TempData = new TempDataDictionary(Http, Mock.Of<ITempDataProvider>())
            };
        }
    }

    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];
        public bool IsAvailable => true;
        public string Id => "COD-TEST-ONLY";
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
