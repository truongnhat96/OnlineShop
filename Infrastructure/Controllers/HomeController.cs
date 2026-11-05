using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using UseCase.Business_Logic;
using UseCase.Recommendations;

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
            var list = (await _homeManage.FindProductAsync(keyword)).ToList();
            if (page == 1 && list.Count > 0)
            {
                try
                {
                    var tracker = HttpContext.RequestServices.GetService<IRecommendationInteractionService>();
                    if (tracker is not null) await tracker.RecordSearchAsync(keyword, HttpContext.RequestAborted);
                }
                catch (Exception error) { _logger.LogWarning("Recommendation search not recorded: {ErrorType}", error.GetType().Name); }
            }

            int pageSize = 10;
            int totalProduct = list.Count();
            var totalPage = (int)Math.Ceiling((double)totalProduct / pageSize);

            var products = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new SearchResultModel
            {
                Keyword = keyword,
                Products = products,
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
