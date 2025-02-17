using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public CategoryRepository(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task<IEnumerable<Entities.Category>> GetAllCategoriesAsync()
        {
            var result = await _context.Categories.ToListAsync();
            return _mapper.Map<IEnumerable<Entities.Category>>(result);
        }

        public async Task<Entities.Category> GetCategoryPostAsync()
        {
            var post = await _context.Categories.FirstOrDefaultAsync(c => c.Id == 10);
            return _mapper.Map<Entities.Category>(post);
        }
    }
}
