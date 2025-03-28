using Entities;
using Infrastructure.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UseCase;
using UseCase.Business_Logic;
using UseCase.MailService;

namespace Infrastructure.Controllers
{
    public class AuthController : Controller
    {
        private readonly IUserManage _userManage;
        private readonly IMailSender _mailSender;

        public AuthController(IUserManage userManage, IMailSender mailSender)
        {
            _userManage = userManage;
            _mailSender = mailSender;
        }
        [HttpGet("/Login")]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost("/Login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _userManage.LoginAsync(model.Username, model.Password);
                    var role = await _userManage.GetRoleNameAsync(user.RoleId);

                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.Sid, user.Id.ToString()),
                        new(ClaimTypes.NameIdentifier, user.Username),
                        new(ClaimTypes.Name, user.DisplayName),
                        new(ClaimTypes.Email, user.Email),
                        new(ClaimTypes.Role, role)
                    };
                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = model.IsRememberLoginInfor,
                        ExpiresUtc = model.IsRememberLoginInfor == true ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddMinutes(25)
                    };
 
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
                    return RedirectToAction("Index", "Home");
                }
                catch (ArgumentNullException)
                {
                    ModelState.AddModelError("LoginError", "Tên người dùng hoặc mật khẩu không chính xác");
                    return View(model);
                }
            }
            return View(model);
        }

        [Route("/Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet("/Signup")]
        public IActionResult Signup()
        {
            return View();
        }

        [HttpPost("/Signup")]
        public async Task<IActionResult> Signup(SignupModel model)
        { 
            var check = await _userManage.Validate(model.Username, model.Password, model.ConfirmPassword, model.Email, ModelState.IsValid);
            switch(check)
            {
                case ValidationResult.UsernameNotValid:
                    ModelState.AddModelError("Username", "Tên người dùng phải chứa ít nhất một chữ hoặc số và không được chứa ký tự đặc biệt");
                    return View(model);
                case ValidationResult.UserExists:
                    ModelState.AddModelError("Username", "Tên người dùng đã tồn tại");
                    return View(model);
                case ValidationResult.EmailExists:
                    ModelState.AddModelError("Email", "Email đã tồn tại");
                    return View(model);
                case ValidationResult.PasswordNotMatch:
                    ModelState.AddModelError("Password", "Dữ liệu mật khẩu không khớp");
                    return View(model);
                case ValidationResult.PasswordTooShort:
                    ModelState.AddModelError("Password", "Mật khẩu phải có ít nhất 5 ký tự");
                    return View(model);
                default:
                    await _userManage.SignUpAsync(new User() 
                    {
                        Username = model.Username,
                        Email = model.Email,
                        DisplayName = model.DisplayName,
                        Password = model.Password,
                        RoleId = 2
                    });
                    return RedirectToAction("Login");
            }
        }

        [HttpGet("/ForgotPassword")]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost("/ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromForm]string UsernameOrEmail)
        {
            try
            {
                var user = await _userManage.GetUserAsync(UsernameOrEmail);
                var result = await _mailSender.SendMailAsync(new MailContent() 
                {
                    To = user.Email,
                    Subject = "Reset Password",
                    Body = HtmlHelper.GenerateHTMLContent(user.Password)
                });
                return result == true ? View("Confirm", user.Email) : View(true);
            }
            catch (ArgumentNullException e)
            {
                ModelState.AddModelError("UsernameOrEmail", e.Message);
                return View();
            }
        }

/*        [HttpGet("/Confirm")]
        public IActionResult Confirm()
        {
            return View();
        }*/

        [HttpGet("/Forbidden")]
        public IActionResult Forbidden()
        {
            return View();
        }
    }
}
