using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer
{
    public class ReviewRepository : IReviewRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public ReviewRepository(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task<Entities.Review> AddReviewAsync(Entities.Review review)
        {
            var entity = _mapper.Map<Entities.Review, Review>(review);
            await _context.Reviews.AddAsync(entity);
            await _context.SaveChangesAsync();
            return review;
        }

        public async Task<IEnumerable<Entities.Review>> GetReviewAsync(int productid)
        {
            var reviews = await _context.Reviews.Where(r => r.ProductId == productid).ToListAsync();
            return _mapper.Map<IEnumerable<Entities.Review>>(reviews);
        }
    }
}
