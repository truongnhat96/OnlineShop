using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UseCase;

namespace Infrastructure.SqlServer
{
    public class ProductRepository : IProductRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public ProductRepository(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task<Entities.Product> AddProductAsync(Entities.Product product)
        {
            var productDb = _mapper.Map<Entities.Product, Product>(product);
            await _context.Products.AddAsync(productDb);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task<Entities.Product> DeleteProductAsync(int id)
        {
            var productDb = await _context.Products.FindAsync(id) ?? throw new ArgumentNullException("Sản phẩm không tồn tại");
            _context.Products.Remove(productDb);
            await _context.SaveChangesAsync();
            return _mapper.Map<Entities.Product>(productDb);
        }

        public async Task<IEnumerable<Entities.Product>> FilterBy(SortingType type, int id)
        {
            var queryResult = await _context.Products.Where(p => p.CategoryId == id).ToListAsync();
            switch (type)
            {
                case SortingType.AscendingPrice:
                    queryResult = await _context.Products.Where(p => p.CategoryId == id).OrderBy(p => p.Price).ToListAsync();
                    break;
                case SortingType.DescendingPrice:
                    queryResult = await _context.Products.Where(p => p.CategoryId == id).OrderByDescending(p => p.Price).ToListAsync();
                    break;
                case SortingType.Popularity:
                    queryResult = await _context.Products.Where(p => p.CategoryId == id).OrderByDescending(p => p.Sold).ToListAsync();
                    break;
                case SortingType.Date:
                    queryResult = await _context.Products.Where(p => p.CategoryId == id).OrderByDescending(p => p.Date_Import).ToListAsync();
                    break;
                case SortingType.Rating:
                    /*var qr = _context.Products.FromSql($"SELECT * FROM Products WHERE CategoryId = {id} ORDER BY (SELECT SUM(Rating) FROM Reviews WHERE ProductId = Products.Id) DESC");
                    queryResult = await qr.ToListAsync();*/
                    var result = from p in _context.Products
                                 where p.CategoryId == id
                                 let sortby = (from r in _context.Reviews
                                               where r.ProductId == p.Id
                                               select r.Rating).Sum()
                                 orderby sortby descending
                                 select p;
                    queryResult = await result.ToListAsync();
                    break;
            }
            return _mapper.Map<IEnumerable<Entities.Product>>(queryResult);
        }

        public async Task<IEnumerable<Entities.Product>> FindProductsAsync(string keyword)
        {
            var productDb = await _context.Products.Where(p => EF.Functions.Collate(p.Name, "SQL_Latin1_General_CP1_CI_AI").Contains(keyword)).ToListAsync();
            return _mapper.Map<IEnumerable<Entities.Product>>(productDb);
        }

        public async Task<IEnumerable<Entities.Product>> FindProductsAsync(double firstPrice, double lastPrice, int id)
        {
            var productsDb = await _context.Products.Where(p => p.Price >= firstPrice && p.Price <= lastPrice && p.CategoryId == id).ToListAsync();
            return _mapper.Map<IEnumerable<Entities.Product>>(productsDb);
        }

        public async Task<Entities.Product> GetProductAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            return _mapper.Map<Entities.Product>(product);
        }

        public async Task<IEnumerable<Entities.Product>> GetProductsAsync()
        {
            var products = await _context.Products.ToListAsync();
            return _mapper.Map<IEnumerable<Entities.Product>>(products);
        }

        public async Task<IEnumerable<Entities.Product>> GetProductsAsync(int categoryId)
        {
            var products = await _context.Products.Where(p => p.CategoryId == categoryId).ToListAsync();
            return _mapper.Map<IEnumerable<Entities.Product>>(products);
        }

        public async Task<Entities.Product> UpdateProductAsync(Entities.Product product)
        {
            var productDb = _mapper.Map<Entities.Product, Product>(product);
            _context.Products.Update(productDb);
            await _context.SaveChangesAsync();
            return product;
        }
    }
}
