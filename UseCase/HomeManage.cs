using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UseCase.Business_Logic;
using UseCase.UnitOfWork;

namespace UseCase
{
    public class HomeManage : IHomeManage
    {
        private readonly ISearchingUnitOfWork _searchUnitOfWork;

        public HomeManage(ISearchingUnitOfWork searchUnitOfWork)
        {
            _searchUnitOfWork = searchUnitOfWork;
        }
        public async Task<IEnumerable<Post>> FindPostAsync(string keyword)
        {
            return await _searchUnitOfWork.PostRepository.FindPostsAsync(keyword);
        }

        public async Task<IEnumerable<Product>> FindProductAsync(string keyword)
        {
            return await _searchUnitOfWork.ProductRepository.FindProductsAsync(keyword);
        }
    }
}
