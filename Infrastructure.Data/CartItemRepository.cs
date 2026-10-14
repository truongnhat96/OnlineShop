using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SqlServer
{
    public class CartItemRepository : ICartItemRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public CartItemRepository(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task AddCartAsync(Entities.CartItem cartItem)
        {
            var itemDb = _mapper.Map<Entities.CartItem, CartItem>(cartItem);
            await _context.CartItems.AddAsync(itemDb);
            await _context.SaveChangesAsync();
        }

        public async Task<Entities.CartItem?> GetCartItemAsync(int userId, int productId)
        {
            var itemDb = await _context.CartItems.AsNoTracking().FirstOrDefaultAsync(item => item.ProductId == productId && item.UserId == userId);
            return _mapper.Map<CartItem?, Entities.CartItem?>(itemDb);
        }

        public async Task<IEnumerable<Entities.CartItem>> GetCartItemsAsync(int userId)
        {
            var itemsDb = await _context.CartItems.AsNoTracking().Where(item => item.UserId == userId).ToListAsync() ?? [];
            return _mapper.Map<IEnumerable<CartItem>, IEnumerable<Entities.CartItem>>(itemsDb);
        }

        public async Task RemoveCartAsync(int productId, int userId)
        {
            var itemDb = await _context.CartItems.FirstOrDefaultAsync(item => item.ProductId == productId && item.UserId == userId) ?? throw new("item not exist");
            _context.CartItems.Remove(itemDb);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCartAsync(Entities.CartItem cartItem)
        {
            // Can Using to update without tracking
            //var existItem = _context.Entry(_context.CartItems.Find(cartItem.Id) ?? throw new());
            //existItem.State = EntityState.Detached;
            var itemDb = _mapper.Map<Entities.CartItem, CartItem>(cartItem);
            _context.CartItems.Update(itemDb);
            await _context.SaveChangesAsync();
        }
    }
}
