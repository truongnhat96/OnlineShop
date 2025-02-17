using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer
{
    public class SearchingUnitOfWork : ISearchingUnitOfWork
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public SearchingUnitOfWork(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;

            ProductRepository = new ProductRepository(context, mapper);
            PostRepository = new PostRepository(context, mapper);
        }
        public IProductRepository ProductRepository { get; }

        public IPostRepository PostRepository { get; }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
