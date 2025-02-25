using System.Diagnostics;
using Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using UseCase.Business_Logic;

namespace Infrastructure.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHomeManage _homeManage;

        public HomeController(IHomeManage homeManage, ILogger<HomeController> logger)
        {
            _logger = logger;
            _homeManage = homeManage;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }


        [HttpGet("/Search", Name = "searchAll")]
        [HttpGet("/Search/page/{page}")]
        public async Task<IActionResult> Search([FromQuery] string keyword, int page = 1)
        {
            var list1 = await _homeManage.FindProductAsync(keyword);
            var list2 = await _homeManage.FindPostAsync(keyword);

            int pageSize = 10;
            int totalItems = list1.Count() + list2.Count();
            int totalPage = (int)Math.Ceiling((double)totalItems / pageSize);

            var products = list1.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var posts = list2.Skip((page - 1) * (pageSize / 2)).Take(pageSize / 2).ToList();

            var model = new SearchResultModel
            {
                Keyword = keyword,
                Products = products,
                Posts = posts,
                currentPage = page,
                totalPage = totalPage
            };
            return View("SearchResult", model);
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
