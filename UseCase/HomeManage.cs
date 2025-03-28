using Entities;
using UseCase.Business_Logic;
using UseCase.Repository;

namespace UseCase
{
    public class HomeManage : IHomeManage
    {
        private readonly IProductRepository _productRepository;

        public HomeManage(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }


        public async Task<IEnumerable<Product>> FindProductAsync(string keyword)
        {
            return await _productRepository.FindProductsAsync(keyword);
        }
    }
}
